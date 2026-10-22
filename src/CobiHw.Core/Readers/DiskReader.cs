using System.Globalization;

namespace CobiHw.Core.Readers;

/// <summary>
/// Reads active time and read/write speed of every physical disk, matching Task Manager's disk graphs.
/// </summary>
internal sealed class DiskReader
{
    private const string TotalInstance = "_Total";

    // "C:" on most PCs; used to flag the disk that holds Windows.
    private static readonly string? SystemDriveLetter = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');

    private readonly PdhCounter? _idleTime;
    private readonly PdhCounter? _readBytes;
    private readonly PdhCounter? _writeBytes;

    /// <summary>Registers the physical disk counters on the shared PDH query.</summary>
    /// <param name="query">Query collected once per sample by <see cref="HardwareMonitor"/>.</param>
    public DiskReader(PdhQuery query)
    {
        _idleTime = query.TryAddCounter(@"\PhysicalDisk(*)\% Idle Time");
        _readBytes = query.TryAddCounter(@"\PhysicalDisk(*)\Disk Read Bytes/sec");
        _writeBytes = query.TryAddCounter(@"\PhysicalDisk(*)\Disk Write Bytes/sec");
    }

    /// <summary>Builds a snapshot from the values collected in the latest query sample.</summary>
    public DiskSnapshot Read() => new(ReadDisks());

    private List<DiskActivity> ReadDisks()
    {
        var disks = new List<DiskActivity>();

        if (_idleTime is null)
        {
            return disks;
        }

        Dictionary<string, double> reads = ByInstance(_readBytes);
        Dictionary<string, double> writes = ByInstance(_writeBytes);

        foreach (var (instance, idlePercent) in _idleTime.GetInstanceValues())
        {
            if (instance == TotalInstance || !TryParseInstance(instance, out int number, out string letters))
            {
                continue;
            }

            // Task Manager's "active time" is the complement of idle time.
            double activePercent = Math.Clamp(100 - idlePercent, 0, 100);
            disks.Add(new DiskActivity(
                number,
                letters,
                activePercent,
                ContainsSystemDrive(letters),
                reads.TryGetValue(instance, out double read) ? read : null,
                writes.TryGetValue(instance, out double write) ? write : null));
        }

        disks.Sort((left, right) => left.Number.CompareTo(right.Number));
        return disks;
    }

    // Byte rates go above 100, so the percent clamp is turned off; duplicate names keep the first value.
    private static Dictionary<string, double> ByInstance(PdhCounter? counter) =>
        counter?.GetInstanceValues(allowAbove100: true)
            .GroupBy(value => value.Instance)
            .ToDictionary(group => group.Key, group => group.First().Value) ?? new Dictionary<string, double>();

    private static bool ContainsSystemDrive(string letters) =>
        !string.IsNullOrEmpty(SystemDriveLetter) &&
        letters.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(SystemDriveLetter, StringComparer.OrdinalIgnoreCase);

    // PDH names physical disks "0 C: D:" (disk number, then its drive letters, if any).
    private static bool TryParseInstance(string instance, out int number, out string letters)
    {
        int separator = instance.IndexOf(' ');
        string numberText = separator < 0 ? instance : instance[..separator];
        letters = separator < 0 ? string.Empty : instance[(separator + 1)..].Trim();

        return int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
