namespace CobiHw.Core;

public sealed record HardwareSnapshot(
    CpuSnapshot Cpu,
    MemorySnapshot? Memory,
    GpuSnapshot Gpu,
    DiskSnapshot Disk,
    NetworkSnapshot Network);

public sealed record CpuSnapshot(string? Name, double? UsagePercent, double? FrequencyGhz);

public sealed record MemorySnapshot(ulong UsedBytes, ulong TotalBytes)
{
    public double UsagePercent => TotalBytes == 0 ? 0 : UsedBytes * 100.0 / TotalBytes;
}

public sealed record GpuSnapshot(string? Name, double? UsagePercent, ulong? MemoryUsedBytes, ulong? MemoryTotalBytes);

public sealed record DiskSnapshot(IReadOnlyList<DiskActivity> Disks)
{
    public DiskActivity? System => Disks.FirstOrDefault(disk => disk.IsSystem);

    public DiskActivity? Find(int number) => Disks.FirstOrDefault(disk => disk.Number == number);
}

public sealed record DiskActivity(int Number, string Letters, double ActivePercent, bool IsSystem);

public sealed record NetworkSnapshot(
    double? DownloadBytesPerSecond,
    double? UploadBytesPerSecond,
    ulong SessionReceivedBytes,
    ulong SessionSentBytes);
