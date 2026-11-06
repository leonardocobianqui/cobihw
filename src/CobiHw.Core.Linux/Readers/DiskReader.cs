using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CobiHw.Core.Linux.Readers;

/// <summary>
/// Reads disk activity from /proc/diskstats, and which disk holds what from /sys/block and /proc/self/mountinfo.
/// </summary>
/// <remarks>
/// Active time is the share of the interval with I/O in flight (the io_ticks counter), the same as %util in iostat and
/// the closest match to Windows' "active time". diskstats counts sectors of 512 bytes, whatever the disk's real sector size.
/// </remarks>
internal sealed class DiskReader(SystemFiles files)
{
    private const double SectorBytes = 512;

    // Mount points under these are the system's own, not places the user keeps files.
    private static readonly string[] SystemMountPoints =
        ["/boot", "/efi", "/snap", "/var", "/usr", "/opt", "/srv", "/root", "/tmp", "/proc", "/sys", "/dev", "/run", "/swap"];

    // Except these, where desktops mount removable drives.
    private static readonly string[] RemovableMountPoints = ["/run/media/", "/media/", "/mnt/"];

    private Dictionary<string, DiskCounters> _previous = [];
    private long _previousTimestamp;

    /// <summary>Reads every physical disk now.</summary>
    /// <param name="timestamp"><see cref="Stopwatch"/> timestamp of the sample.</param>
    /// <returns>Active time is 0 and speeds are <see langword="null"/> on the first call, which only sets the baseline.</returns>
    public DiskSnapshot Read(long timestamp)
    {
        BlockDevices devices = BlockDevices.Read(files);
        Dictionary<string, List<string>> volumes = Volumes(devices, files.Text("proc/self/mountinfo"));
        Dictionary<string, DiskCounters> counters = ParseDiskStats(files.Text("proc/diskstats"));
        double? seconds = _previousTimestamp == 0 ? null : Stopwatch.GetElapsedTime(_previousTimestamp, timestamp).TotalSeconds;

        var disks = new List<DiskActivity>(devices.Disks.Count);
        for (int number = 0; number < devices.Disks.Count; number++)
        {
            string name = devices.Disks[number];
            List<string> mounted = volumes.GetValueOrDefault(name) ?? [];
            DiskCounters? now = counters.GetValueOrDefault(name);
            DiskCounters? before = _previous.GetValueOrDefault(name);
            bool measured = seconds > 0 && now is not null && before is not null;

            disks.Add(new DiskActivity(
                number,
                string.Join(' ', mounted),
                measured ? Math.Clamp((now!.IoMilliseconds - before!.IoMilliseconds) / (seconds!.Value * 1000) * 100, 0, 100) : 0,
                mounted.Contains("/"),
                measured ? Rate(now!.SectorsRead, before!.SectorsRead, seconds!.Value) : null,
                measured ? Rate(now!.SectorsWritten, before!.SectorsWritten, seconds!.Value) : null));
        }

        _previous = counters;
        _previousTimestamp = timestamp;
        return new DiskSnapshot(disks);
    }

    /// <summary>Counters per device name from the text of /proc/diskstats.</summary>
    /// <remarks>Columns: major, minor, name, then reads, reads merged, sectors read (6th), …, sectors written (10th), …, ms doing I/O (13th).</remarks>
    internal static Dictionary<string, DiskCounters> ParseDiskStats(string? diskstats)
    {
        var counters = new Dictionary<string, DiskCounters>(StringComparer.Ordinal);
        foreach (string line in (diskstats ?? string.Empty).Split('\n'))
        {
            string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 13 &&
                double.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out double sectorsRead) &&
                double.TryParse(fields[9], NumberStyles.None, CultureInfo.InvariantCulture, out double sectorsWritten) &&
                double.TryParse(fields[12], NumberStyles.None, CultureInfo.InvariantCulture, out double ioMilliseconds))
            {
                counters[fields[2]] = new DiskCounters(sectorsRead, sectorsWritten, ioMilliseconds);
            }
        }

        return counters;
    }

    /// <summary>The user-facing mount points of each disk, "/" first, from the text of /proc/self/mountinfo.</summary>
    /// <remarks>
    /// A mount is traced to its disk by device number. btrfs reports a device number of its own, so the mount source
    /// (/dev/nvme0n1p2, /dev/mapper/luks-…) is the fallback. Encrypted and LVM volumes are followed down to the
    /// partitions they sit on.
    /// </remarks>
    internal static Dictionary<string, List<string>> Volumes(BlockDevices devices, string? mountinfo)
    {
        var volumes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string line in (mountinfo ?? string.Empty).Split('\n'))
        {
            // "36 25 259:2 / / rw,relatime shared:1 - ext4 /dev/nvme0n1p2 rw": optional fields end at " - ".
            string[] fields = line.Split(' ');
            int separator = Array.IndexOf(fields, "-");
            if (fields.Length < 5 || separator < 0 || separator + 2 >= fields.Length)
            {
                continue;
            }

            string mountPoint = Unescape(fields[4]);
            if (!IsUserFacing(mountPoint))
            {
                continue;
            }

            IReadOnlySet<string> disks = devices.DisksOfDevice(fields[2]);
            if (disks.Count == 0 && fields[separator + 2].StartsWith("/dev/", StringComparison.Ordinal))
            {
                disks = devices.DisksOfSource(fields[separator + 2]);
            }

            foreach (string disk in disks)
            {
                List<string> list = volumes.TryGetValue(disk, out List<string>? found) ? found : volumes[disk] = [];
                if (!list.Contains(mountPoint))
                {
                    list.Add(mountPoint);
                }
            }
        }

        foreach (List<string> list in volumes.Values)
        {
            list.Sort((left, right) => left == "/" ? -1 : right == "/" ? 1 : string.CompareOrdinal(left, right));
        }

        return volumes;
    }

    private static bool IsUserFacing(string mountPoint) =>
        mountPoint == "/" ||
        RemovableMountPoints.Any(prefix => mountPoint.StartsWith(prefix, StringComparison.Ordinal)) ||
        !SystemMountPoints.Any(prefix => mountPoint == prefix || mountPoint.StartsWith(prefix + "/", StringComparison.Ordinal));

    // mountinfo escapes space, tab, newline and backslash as octal: "\040" is a space.
    private static string Unescape(string text)
    {
        if (!text.Contains('\\'))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 3 < text.Length && IsOctal(text[i + 1]) && IsOctal(text[i + 2]) && IsOctal(text[i + 3]))
            {
                result.Append((char)Convert.ToInt32(text.Substring(i + 1, 3), 8));
                i += 3;
            }
            else
            {
                result.Append(text[i]);
            }
        }

        return result.ToString();
    }

    private static bool IsOctal(char character) => character is >= '0' and <= '7';

    private static double Rate(double now, double before, double seconds) =>
        Math.Max(0, now - before) * SectorBytes / seconds;
}

/// <summary>Cumulative counters of one disk from /proc/diskstats.</summary>
internal sealed record DiskCounters(double SectorsRead, double SectorsWritten, double IoMilliseconds);

/// <summary>
/// The block devices in /sys/block: which are physical disks, which partitions belong to them, and what encrypted, LVM
/// or RAID devices sit on top of them.
/// </summary>
internal sealed class BlockDevices
{
    // Device number ("259:2") and name of every disk, partition and virtual block device.
    private readonly Dictionary<string, string> _nameByNumber = new(StringComparer.Ordinal);
    // Name of every partition and of every disk (to itself) → its disk.
    private readonly Dictionary<string, string> _diskByName = new(StringComparer.Ordinal);
    // Virtual devices (dm-0, md0) → the devices under them.
    private readonly Dictionary<string, IReadOnlyList<string>> _slaves = new(StringComparer.Ordinal);
    // Device-mapper names ("luks-…", "ubuntu--vg-root") → dm-N, for /dev/mapper sources.
    private readonly Dictionary<string, string> _mapperNames = new(StringComparer.Ordinal);

    private BlockDevices(IReadOnlyList<string> disks) => Disks = disks;

    /// <summary>Physical disks, in kernel name order; the position is the disk number.</summary>
    public IReadOnlyList<string> Disks { get; }

    /// <summary>Lists the block devices under sys/block.</summary>
    /// <remarks>
    /// A physical disk has a "device" link to its hardware and a size. Loop, zram and device-mapper devices have no
    /// "device"; optical drives (sr*) and empty card readers (size 0) are left out, as Windows leaves them out of its disks.
    /// </remarks>
    public static BlockDevices Read(SystemFiles files)
    {
        IReadOnlyList<string> entries = files.Entries("sys/block");
        List<string> disks =
        [
            .. entries.Where(name =>
                files.Exists($"sys/block/{name}/device") &&
                !name.StartsWith("sr", StringComparison.Ordinal) &&
                files.Number($"sys/block/{name}/size") > 0),
        ];

        var devices = new BlockDevices(disks);
        foreach (string name in entries)
        {
            devices.Add(files.Text($"sys/block/{name}/dev"), name);
            if (disks.Contains(name))
            {
                devices._diskByName[name] = name;
                foreach (string partition in files.Entries($"sys/block/{name}").Where(entry => files.Exists($"sys/block/{name}/{entry}/partition")))
                {
                    devices.Add(files.Text($"sys/block/{name}/{partition}/dev"), partition);
                    devices._diskByName[partition] = name;
                }
            }

            IReadOnlyList<string> slaves = files.Entries($"sys/block/{name}/slaves");
            if (slaves.Count > 0)
            {
                devices._slaves[name] = slaves;
            }

            if (files.Text($"sys/block/{name}/dm/name") is { Length: > 0 } mapperName)
            {
                devices._mapperNames[mapperName] = name;
            }
        }

        return devices;
    }

    /// <summary>The disks under a device number from mountinfo ("259:2"); empty when it isn't a block device (btrfs, tmpfs…).</summary>
    public IReadOnlySet<string> DisksOfDevice(string number) =>
        _nameByNumber.TryGetValue(number, out string? name) ? DisksOf(name, 0) : new HashSet<string>();

    /// <summary>The disks under a mount source such as "/dev/nvme0n1p2" or "/dev/mapper/luks-…".</summary>
    public IReadOnlySet<string> DisksOfSource(string source)
    {
        string name = source["/dev/".Length..];
        return name.StartsWith("mapper/", StringComparison.Ordinal)
            ? _mapperNames.TryGetValue(name["mapper/".Length..], out string? device) ? DisksOf(device, 0) : new HashSet<string>()
            : DisksOf(name, 0);
    }

    private void Add(string? number, string name)
    {
        if (!string.IsNullOrEmpty(number))
        {
            _nameByNumber[number] = name;
        }
    }

    // Follows virtual devices down to physical disks; the depth limit guards against a malformed tree.
    private HashSet<string> DisksOf(string name, int depth)
    {
        if (_diskByName.TryGetValue(name, out string? disk))
        {
            return [disk];
        }

        HashSet<string> disks = [];
        if (depth < 8 && _slaves.TryGetValue(name, out IReadOnlyList<string>? slaves))
        {
            foreach (string slave in slaves)
            {
                disks.UnionWith(DisksOf(slave, depth + 1));
            }
        }

        return disks;
    }
}
