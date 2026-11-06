using System.Globalization;

namespace CobiHw.Core.Linux.Readers;

/// <summary>
/// Reads memory usage from /proc/meminfo.
/// </summary>
/// <remarks>
/// Used memory is MemTotal minus MemAvailable, as free and the GNOME System Monitor report it. MemAvailable is the kernel's
/// estimate of what can be given to new programs without swapping, page cache included; MemFree alone would make a
/// healthy system look full.
/// </remarks>
internal static class MemoryReader
{
    /// <summary>Reads used and total memory.</summary>
    /// <returns>The memory state, or <see langword="null"/> when /proc/meminfo can't be read.</returns>
    public static MemorySnapshot? Read(SystemFiles files) => Parse(files.Text("proc/meminfo"));

    /// <summary>Used and total memory from the text of /proc/meminfo, whose values are in KiB.</summary>
    internal static MemorySnapshot? Parse(string? meminfo)
    {
        Dictionary<string, ulong> values = [];
        foreach (string line in (meminfo ?? string.Empty).Split('\n'))
        {
            string[] parts = line.Split(':', 2);
            string[] amount = parts.Length == 2 ? parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
            if (amount.Length > 0 && ulong.TryParse(amount[0], NumberStyles.None, CultureInfo.InvariantCulture, out ulong kibibytes))
            {
                values[parts[0]] = kibibytes * 1024;
            }
        }

        return values.TryGetValue("MemTotal", out ulong total) && values.TryGetValue("MemAvailable", out ulong available)
            ? new MemorySnapshot(total - Math.Min(available, total), total)
            : null;
    }
}
