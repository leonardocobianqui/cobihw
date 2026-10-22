namespace CobiHw.Core;

/// <summary>
/// Everything read in one sample. Values that couldn't be read are <see langword="null"/>.
/// </summary>
/// <param name="Cpu">Processor.</param>
/// <param name="Memory">RAM, or null if Windows refused the call.</param>
/// <param name="Gpu">Every hardware GPU.</param>
/// <param name="Disk">Every physical disk.</param>
/// <param name="Network">Totals and per-device traffic.</param>
/// <param name="Battery">System battery, or null on PCs without one.</param>
public sealed record HardwareSnapshot(
    CpuSnapshot Cpu,
    MemorySnapshot? Memory,
    GpuSnapshot Gpu,
    DiskSnapshot Disk,
    NetworkSnapshot Network,
    BatterySnapshot? Battery = null);

/// <summary>Processor reading.</summary>
/// <param name="Name">Name from the registry (e.g. "Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz").</param>
/// <param name="UsagePercent">Usage, 0 to 100.</param>
/// <param name="FrequencyGhz">Current clock in GHz, boost included.</param>
public sealed record CpuSnapshot(string? Name, double? UsagePercent, double? FrequencyGhz);

/// <summary>Physical memory reading.</summary>
/// <param name="UsedBytes">Memory in use.</param>
/// <param name="TotalBytes">Memory installed and usable by Windows.</param>
public sealed record MemorySnapshot(ulong UsedBytes, ulong TotalBytes)
{
    /// <summary>Used share of total, 0 to 100.</summary>
    public double UsagePercent => TotalBytes == 0 ? 0 : UsedBytes * 100.0 / TotalBytes;
}

/// <summary>All hardware GPUs read in one sample.</summary>
/// <param name="Gpus">One entry per GPU, in DXGI enumeration order.</param>
public sealed record GpuSnapshot(IReadOnlyList<GpuActivity> Gpus)
{
    /// <summary>The GPU Windows prefers for high performance; what "Automatic" shows.</summary>
    public GpuActivity? Preferred => Gpus.MinBy(gpu => gpu.Rank);

    /// <summary>The GPU with this identity, or null if it's gone (e.g. eGPU unplugged).</summary>
    public GpuActivity? Find(GpuId id) => Gpus.FirstOrDefault(gpu => gpu.Id == id);
}

/// <summary>Identity of a GPU that survives reboots and driver updates, unlike the LUID.</summary>
/// <param name="VendorId">PCI vendor id.</param>
/// <param name="DeviceId">PCI device id (the model).</param>
/// <param name="Instance">0, 1… for identical cards in the same PC.</param>
public readonly record struct GpuId(uint VendorId, uint DeviceId, int Instance);

/// <summary>One GPU's reading.</summary>
/// <param name="Id">Stable identity, used to save the user's choice.</param>
/// <param name="Name">Name reported by the driver.</param>
/// <param name="Rank">Position in Windows' high-performance order (0 = preferred).</param>
/// <param name="IsIntegrated">True when the GPU shares system RAM.</param>
/// <param name="UsagePercent">Busiest engine, 0 to 100.</param>
/// <param name="DedicatedUsedBytes">Dedicated memory in use.</param>
/// <param name="DedicatedTotalBytes">Dedicated memory size (a small reserve on integrated GPUs).</param>
/// <param name="SharedUsedBytes">System RAM in use by an integrated GPU; null on dedicated GPUs.</param>
/// <param name="TemperatureCelsius">Temperature, or null when the driver doesn't report it.</param>
public sealed record GpuActivity(
    GpuId Id,
    string Name,
    int Rank,
    bool IsIntegrated,
    double? UsagePercent,
    ulong? DedicatedUsedBytes,
    ulong DedicatedTotalBytes,
    ulong? SharedUsedBytes,
    double? TemperatureCelsius = null)
{
    /// <summary>Dedicated plus shared memory in use; null when neither was read.</summary>
    public ulong? TotalUsedBytes =>
        DedicatedUsedBytes is null && SharedUsedBytes is null ? null : (DedicatedUsedBytes ?? 0) + (SharedUsedBytes ?? 0);
}

/// <summary>All physical disks read in one sample.</summary>
/// <param name="Disks">One entry per disk, sorted by disk number.</param>
public sealed record DiskSnapshot(IReadOnlyList<DiskActivity> Disks)
{
    /// <summary>The disk that holds Windows, if it was identified.</summary>
    public DiskActivity? System => Disks.FirstOrDefault(disk => disk.IsSystem);

    /// <summary>The disk with this number, or null if it's gone.</summary>
    public DiskActivity? Find(int number) => Disks.FirstOrDefault(disk => disk.Number == number);
}

/// <summary>One physical disk's reading.</summary>
/// <param name="Number">Disk number in Disk Management (0, 1…).</param>
/// <param name="Letters">Drive letters on this disk, e.g. "C: D:"; empty when none.</param>
/// <param name="ActivePercent">Active time, 0 to 100.</param>
/// <param name="IsSystem">True for the disk that holds Windows.</param>
/// <param name="ReadBytesPerSecond">Read speed, or null if the counter is missing.</param>
/// <param name="WriteBytesPerSecond">Write speed, or null if the counter is missing.</param>
public sealed record DiskActivity(
    int Number,
    string Letters,
    double ActivePercent,
    bool IsSystem,
    double? ReadBytesPerSecond = null,
    double? WriteBytesPerSecond = null);

/// <summary>System battery reading.</summary>
/// <param name="Percent">Charge, 0 to 100, or null when unknown.</param>
/// <param name="IsCharging">True while charging.</param>
/// <param name="OnPower">True when plugged in.</param>
/// <param name="Remaining">Windows' estimate of time left on battery, or null when unknown.</param>
public sealed record BatterySnapshot(int? Percent, bool IsCharging, bool OnPower, TimeSpan? Remaining);

/// <summary>Network reading.</summary>
/// <param name="DownloadBytesPerSecond">Sum of physical adapters; null on the first sample.</param>
/// <param name="UploadBytesPerSecond">Sum of physical adapters; null on the first sample.</param>
/// <param name="SessionReceivedBytes">Received since the session started (physical adapters).</param>
/// <param name="SessionSentBytes">Sent since the session started (physical adapters).</param>
/// <param name="Devices">Every interface, including virtual ones (VPNs).</param>
public sealed record NetworkSnapshot(
    double? DownloadBytesPerSecond,
    double? UploadBytesPerSecond,
    ulong SessionReceivedBytes,
    ulong SessionSentBytes,
    IReadOnlyList<DeviceTraffic> Devices)
{
    /// <summary>The device with this interface GUID, or null if it's gone.</summary>
    public DeviceTraffic? Find(Guid id) => Devices.FirstOrDefault(device => device.Id == id);
}

/// <summary>One network device's reading.</summary>
/// <param name="Id">Interface GUID.</param>
/// <param name="Name">Alias shown in Windows settings.</param>
/// <param name="IsUp">True when connected.</param>
/// <param name="DownloadBytesPerSecond">Null on the first sample.</param>
/// <param name="UploadBytesPerSecond">Null on the first sample.</param>
/// <param name="SessionReceivedBytes">Received since the session started.</param>
/// <param name="SessionSentBytes">Sent since the session started.</param>
/// <param name="TotalReceivedBytes">Windows' cumulative counter.</param>
/// <param name="TotalSentBytes">Windows' cumulative counter.</param>
public sealed record DeviceTraffic(
    Guid Id,
    string Name,
    bool IsUp,
    double? DownloadBytesPerSecond,
    double? UploadBytesPerSecond,
    ulong SessionReceivedBytes,
    ulong SessionSentBytes,
    ulong TotalReceivedBytes,
    ulong TotalSentBytes);
