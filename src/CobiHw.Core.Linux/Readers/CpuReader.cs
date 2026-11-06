using System.Globalization;

namespace CobiHw.Core.Linux.Readers;

/// <summary>
/// Reads processor usage from /proc/stat, the current clock from cpufreq and the name from /proc/cpuinfo.
/// </summary>
/// <remarks>Usage is the busy share of the time since the previous sample, as top and the GNOME System Monitor compute it.</remarks>
internal sealed class CpuReader(SystemFiles files)
{
    private CpuTimes? _previous;

    /// <summary>Processor name, read once: it doesn't change while the system runs.</summary>
    public string? Name { get; } = ReadName(files);

    /// <summary>Usage since the previous call and the current clock.</summary>
    /// <returns>Usage is <see langword="null"/> on the first call, which only sets the baseline.</returns>
    public CpuSnapshot Read()
    {
        CpuTimes? current = ParseTimes(files.Text("proc/stat"));
        double? usage = _previous is CpuTimes previous && current is CpuTimes now ? Usage(previous, now) : null;
        _previous = current ?? _previous;
        return new CpuSnapshot(Name, usage, ReadFrequencyGhz());
    }

    /// <summary>Busy and total time from the aggregate "cpu" line of /proc/stat.</summary>
    /// <remarks>
    /// The line is "cpu user nice system idle iowait irq softirq steal guest guest_nice", in USER_HZ ticks. Guest time is
    /// already inside user and nice, so only the first eight count. Time waiting for I/O counts as idle.
    /// </remarks>
    internal static CpuTimes? ParseTimes(string? stat)
    {
        string? line = stat?.Split('\n').FirstOrDefault(text => text.StartsWith("cpu ", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 5)
        {
            return null;
        }

        double total = 0;
        double idle = 0;
        for (int i = 1; i < Math.Min(fields.Length, 9); i++)
        {
            if (!double.TryParse(fields[i], NumberStyles.None, CultureInfo.InvariantCulture, out double ticks))
            {
                return null;
            }

            total += ticks;
            if (i is 4 or 5)
            {
                idle += ticks;
            }
        }

        return new CpuTimes(total - idle, total);
    }

    /// <summary>Busy share of the time between two samples, 0 to 100.</summary>
    /// <remarks>The kernel documents iowait as unreliable: it can go backwards, so the result is kept within 0 and 100.</remarks>
    internal static double? Usage(CpuTimes previous, CpuTimes current)
    {
        double total = current.Total - previous.Total;
        return total <= 0 ? null : Math.Clamp((current.Busy - previous.Busy) / total * 100, 0, 100);
    }

    /// <summary>Average current clock of the online cores, in GHz.</summary>
    /// <remarks>
    /// cpufreq reports kHz per core. Without cpufreq (common in VMs), /proc/cpuinfo may still list "cpu MHz"; without
    /// either, the clock is unknown.
    /// </remarks>
    private double? ReadFrequencyGhz()
    {
        long[] kilohertz =
        [
            .. files.Entries("sys/devices/system/cpu")
                .Where(IsCoreFolder)
                .Select(core => files.Number($"sys/devices/system/cpu/{core}/cpufreq/scaling_cur_freq"))
                .OfType<long>()
                .Where(value => value > 0),
        ];

        if (kilohertz.Length > 0)
        {
            return kilohertz.Average() / 1_000_000;
        }

        double[] megahertz = [.. CpuInfoValues(files.Text("proc/cpuinfo"), "cpu MHz").Select(ParseDouble).OfType<double>()];
        return megahertz.Length > 0 ? megahertz.Average() / 1_000 : null;
    }

    /// <summary>The processor's marketing name.</summary>
    /// <remarks>
    /// x86 has "model name" in /proc/cpuinfo. Many ARM systems don't; there the board model from the device tree is the
    /// closest thing (e.g. "Raspberry Pi 5 Model B Rev 1.0").
    /// </remarks>
    internal static string? ReadName(SystemFiles files)
    {
        string? cpuinfo = files.Text("proc/cpuinfo");
        string? model = files.Text("sys/firmware/devicetree/base/model");
        return CpuInfoValues(cpuinfo, "model name").FirstOrDefault()
            ?? CpuInfoValues(cpuinfo, "Hardware").FirstOrDefault()
            ?? (string.IsNullOrEmpty(model) ? null : model);
    }

    // Values of "key : value" lines in /proc/cpuinfo, one per core for per-core keys.
    private static IEnumerable<string> CpuInfoValues(string? cpuinfo, string key) =>
        (cpuinfo ?? string.Empty)
            .Split('\n')
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && parts[0].Trim() == key && parts[1].Trim().Length > 0)
            .Select(parts => parts[1].Trim());

    private static bool IsCoreFolder(string name) =>
        name.Length > 3 && name.StartsWith("cpu", StringComparison.Ordinal) && name[3..].All(char.IsAsciiDigit);

    private static double? ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
}

/// <summary>Cumulative busy and total CPU time, in ticks.</summary>
internal readonly record struct CpuTimes(double Busy, double Total);
