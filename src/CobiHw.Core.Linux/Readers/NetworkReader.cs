using System.Diagnostics;

namespace CobiHw.Core.Linux.Readers;

/// <summary>
/// Reads network speed and session totals per interface from /sys/class/net.
/// </summary>
/// <remarks>
/// Only physical adapters that are up count in the widget total, as on Windows: a VPN such as Tailscale carries traffic
/// that already went through the Wi-Fi, so counting both would double it. An interface is physical when it has a
/// "device" link to hardware; loopback, tunnels, bridges and containers' veth pairs don't. The speed and session math is
/// shared with Windows, in <see cref="NetworkTraffic"/>.
/// </remarks>
internal sealed class NetworkReader
{
    private readonly SystemFiles _files;
    private readonly NetworkTraffic _traffic;

    /// <summary>Starts a reader, resuming the saved session when it belongs to the current boot.</summary>
    /// <param name="files">Where to read /proc and /sys.</param>
    /// <param name="savedSession">Session saved by a previous run, or <see langword="null"/>.</param>
    /// <remarks>
    /// The boot is identified by btime in /proc/stat, the wall-clock time of the boot. Deriving it from the uptime, as on
    /// Windows, doesn't work here: Linux's uptime stops while suspended, so every suspend would look like a new boot.
    /// </remarks>
    public NetworkReader(SystemFiles files, NetworkSessionState? savedSession)
    {
        _files = files;
        DateTime? bootTime = BootTimeUtc(files.Text("proc/stat"));
        NetworkSessionState session = bootTime is DateTime boot
            ? savedSession is not null && savedSession.BelongsToBoot(boot) ? savedSession : NetworkSessionState.CreateForBoot(boot)
            : savedSession is not null && savedSession.BelongsToCurrentBoot() ? savedSession : NetworkSessionState.CreateForCurrentBoot();
        _traffic = new NetworkTraffic(session);
    }

    /// <summary>Reads every interface now and returns speeds and session totals.</summary>
    /// <param name="timestamp"><see cref="Stopwatch"/> timestamp of the sample.</param>
    public NetworkSnapshot Read(long timestamp) => _traffic.Process(ReadInterfaces(), timestamp);

    /// <summary>Zeroes the session totals of one device, or the widget total when <paramref name="device"/> is null.</summary>
    public void ResetSession(Guid? device) => _traffic.ResetSession(device);

    /// <summary>Copy of the session state to save on exit.</summary>
    public NetworkSessionState ExportSession() => _traffic.ExportSession();

    /// <summary>Boot time from the "btime" line of /proc/stat, in seconds since the Unix epoch.</summary>
    internal static DateTime? BootTimeUtc(string? stat)
    {
        string? line = stat?.Split('\n').FirstOrDefault(text => text.StartsWith("btime ", StringComparison.Ordinal));
        return long.TryParse(line?["btime ".Length..].Trim(), out long seconds)
            ? DateTime.UnixEpoch.AddSeconds(seconds)
            : null;
    }

    private List<InterfaceSample> ReadInterfaces()
    {
        var interfaces = new List<InterfaceSample>();
        foreach (string name in _files.Entries("sys/class/net"))
        {
            string folder = $"sys/class/net/{name}";
            if (_files.Number($"{folder}/ifindex") is not long index ||
                _files.Number($"{folder}/statistics/rx_bytes") is not long received ||
                _files.Number($"{folder}/statistics/tx_bytes") is not long sent)
            {
                // Gone between the listing and the read.
                continue;
            }

            bool isUp = IsUp(_files.Text($"{folder}/operstate"), _files.Number($"{folder}/carrier"));
            bool isPhysical = _files.Exists($"{folder}/device");
            interfaces.Add(new InterfaceSample((ulong)index, InterfaceId.From(name), name, isUp, isPhysical && isUp, (ulong)received, (ulong)sent));
        }

        return interfaces;
    }

    /// <summary>True when the interface is connected.</summary>
    /// <remarks>
    /// operstate is "up" for a working link. Drivers that don't track it report "unknown"; for those, the carrier
    /// attribute (1 = link detected) decides.
    /// </remarks>
    internal static bool IsUp(string? operstate, long? carrier) =>
        operstate == "up" || (operstate == "unknown" && carrier == 1);
}
