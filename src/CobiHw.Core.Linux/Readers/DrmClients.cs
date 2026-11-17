using System.Globalization;

namespace CobiHw.Core.Linux.Readers;

/// <summary>
/// GPU time and memory per device, added up from the DRM client statistics every process publishes in /proc/*/fdinfo.
/// </summary>
/// <remarks>
/// This is the kernel's driver-neutral source (Documentation/gpu/drm-usage-stats), used for Intel and the other drivers
/// without a whole-GPU usage file. Only processes of the same user can be read: desktops, browsers and games run as the
/// user, so nearly all GPU work is seen; work of root or of other users' sessions isn't.
/// </remarks>
internal sealed class DrmClients(SystemFiles files)
{
    // Busy time per engine and client in the previous sample, to subtract.
    private Dictionary<(string Device, string Client), Dictionary<string, double>> _previous = [];
    private long _previousTimestamp;

    /// <summary>Reads every client now.</summary>
    /// <param name="devices">PCI slots to read, e.g. "0000:00:02.0"; other devices are skipped.</param>
    /// <param name="timestamp"><see cref="System.Diagnostics.Stopwatch"/> timestamp of the sample.</param>
    /// <returns>Usage and memory per PCI slot; usage is <see langword="null"/> on the first call, which only sets the baseline.</returns>
    public Dictionary<string, DrmUsage> Read(IReadOnlySet<string> devices, long timestamp)
    {
        Dictionary<(string Device, string Client), DrmClient> clients = ReadClients(devices);
        double? elapsedNanoseconds = _previousTimestamp == 0
            ? null
            : System.Diagnostics.Stopwatch.GetElapsedTime(_previousTimestamp, timestamp).TotalNanoseconds;

        var usage = new Dictionary<string, DrmUsage>(StringComparer.Ordinal);
        foreach (string device in devices)
        {
            List<KeyValuePair<(string Device, string Client), DrmClient>> ofDevice = [.. clients.Where(client => client.Key.Device == device)];
            usage[device] = new DrmUsage(
                elapsedNanoseconds > 0 ? BusiestEngine(ofDevice, elapsedNanoseconds.Value) : null,
                Sum(ofDevice, client => client.LocalBytes),
                Sum(ofDevice, client => client.SystemBytes));
        }

        _previous = clients.ToDictionary(client => client.Key, client => client.Value.EngineNanoseconds);
        _previousTimestamp = timestamp;
        return usage;
    }

    /// <summary>One client's statistics from the text of an fdinfo file; null when it isn't a GPU client.</summary>
    /// <remarks>
    /// Keys used: drm-pdev (the PCI slot), drm-client-id, drm-engine-&lt;name&gt; (busy ns), drm-engine-capacity-&lt;name&gt;
    /// (how many engines of that kind share the counter) and drm-resident-&lt;region&gt; (memory in use). Regions named
    /// vram* or local* are the GPU's own memory; the rest (system, gtt, cpu) is system RAM.
    /// </remarks>
    internal static (string Device, string Client, DrmClient Statistics)? Parse(string? fdinfo)
    {
        string? device = null;
        string? client = null;
        var engines = new Dictionary<string, double>(StringComparer.Ordinal);
        var capacity = new Dictionary<string, double>(StringComparer.Ordinal);
        ulong? local = null;
        ulong? system = null;

        foreach (string line in (fdinfo ?? string.Empty).Split('\n'))
        {
            string[] parts = line.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            string key = parts[0].Trim();
            string value = parts[1].Trim();
            if (key == "drm-pdev")
            {
                device = value;
            }
            else if (key == "drm-client-id")
            {
                client = value;
            }
            else if (key.StartsWith("drm-engine-capacity-", StringComparison.Ordinal) && Number(value) is double count)
            {
                capacity[key["drm-engine-capacity-".Length..]] = count;
            }
            else if (key.StartsWith("drm-engine-", StringComparison.Ordinal) && Number(value) is double nanoseconds)
            {
                engines[key["drm-engine-".Length..]] = nanoseconds;
            }
            else if (key.StartsWith("drm-resident-", StringComparison.Ordinal) && Bytes(value) is ulong bytes)
            {
                string region = key["drm-resident-".Length..];
                if (region.StartsWith("vram", StringComparison.Ordinal) || region.StartsWith("local", StringComparison.Ordinal))
                {
                    local = (local ?? 0) + bytes;
                }
                else
                {
                    system = (system ?? 0) + bytes;
                }
            }
        }

        if (device is null || client is null)
        {
            return null;
        }

        // A counter shared by several engines of one kind can reach N × the elapsed time; scale it back to one engine.
        foreach ((string engine, double count) in capacity)
        {
            if (count > 1 && engines.TryGetValue(engine, out double nanoseconds))
            {
                engines[engine] = nanoseconds / count;
            }
        }

        return (device, client, new DrmClient(engines, local, system));
    }

    // Each open file of a process has an fdinfo, and several can point to the same client (dup'd or inherited
    // descriptors): the client id makes each one count once.
    private Dictionary<(string Device, string Client), DrmClient> ReadClients(IReadOnlySet<string> devices)
    {
        var clients = new Dictionary<(string Device, string Client), DrmClient>();
        foreach (string process in files.Entries("proc").Where(name => name.All(char.IsAsciiDigit)))
        {
            foreach (string descriptor in files.Entries($"proc/{process}/fdinfo"))
            {
                // Real /proc fds are links; only those to /dev/dri are GPU clients, which saves reading every socket and file.
                string? target = files.LinkTarget($"proc/{process}/fd/{descriptor}");
                if (target is not null && !target.StartsWith("/dev/dri/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Parse(files.Text($"proc/{process}/fdinfo/{descriptor}")) is var (device, client, statistics) && devices.Contains(device))
                {
                    clients.TryAdd((device, client), statistics);
                }
            }
        }

        return clients;
    }

    // Busy time of each engine across clients, as a share of the interval; the busiest engine wins, as on Windows.
    // A client first seen in this sample opened during the interval, so all of its time counts.
    private double? BusiestEngine(List<KeyValuePair<(string Device, string Client), DrmClient>> clients, double elapsedNanoseconds)
    {
        var busy = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach ((var key, DrmClient client) in clients)
        {
            Dictionary<string, double>? before = _previous.GetValueOrDefault(key);
            foreach ((string engine, double nanoseconds) in client.EngineNanoseconds)
            {
                double delta = Math.Max(0, nanoseconds - (before?.GetValueOrDefault(engine) ?? 0));
                busy[engine] = busy.GetValueOrDefault(engine) + delta;
            }
        }

        return busy.Count == 0 ? 0 : Math.Clamp(busy.Values.Max() / elapsedNanoseconds * 100, 0, 100);
    }

    private static ulong? Sum(List<KeyValuePair<(string Device, string Client), DrmClient>> clients, Func<DrmClient, ulong?> bytes)
    {
        ulong?[] values = [.. clients.Select(client => bytes(client.Value)).Where(value => value is not null)];
        return values.Length == 0 ? null : values.Aggregate((ulong)0, (total, value) => total + value!.Value);
    }

    private static double? Number(string text) =>
        double.TryParse(text.Split(' ')[0], NumberStyles.None, CultureInfo.InvariantCulture, out double value) ? value : null;

    // "412936 KiB"; the unit is optional and can be KiB or MiB.
    private static ulong? Bytes(string text)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !ulong.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out ulong value))
        {
            return null;
        }

        return parts.Length < 2 ? value : parts[1] switch
        {
            "KiB" => value * 1024,
            "MiB" => value * 1024 * 1024,
            _ => value,
        };
    }
}

/// <summary>One GPU client's cumulative engine time and current memory.</summary>
internal sealed record DrmClient(Dictionary<string, double> EngineNanoseconds, ulong? LocalBytes, ulong? SystemBytes);

/// <summary>What the clients of one GPU add up to.</summary>
/// <param name="UsagePercent">Busiest engine over the interval, 0 to 100.</param>
/// <param name="LocalBytes">GPU memory in use; null when no client reported it.</param>
/// <param name="SystemBytes">System RAM in use by the GPU; null when no client reported it.</param>
internal sealed record DrmUsage(double? UsagePercent, ulong? LocalBytes, ulong? SystemBytes);
