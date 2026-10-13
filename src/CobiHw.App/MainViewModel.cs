using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Settings;
using CobiHw.Core;

namespace CobiHw.App;

public sealed partial class MainViewModel : ObservableObject
{
    private DisplaySettings _display;
    private ColorSettings _colors;
    private HardwareSnapshot? _lastSnapshot;

    public MainViewModel(DisplaySettings display, ColorSettings colors)
    {
        _display = display;
        _colors = colors;
        ApplyDisplay();
    }

    [ObservableProperty] private ViewLayout _layout;

    [ObservableProperty] private bool _showCpu;
    [ObservableProperty] private bool _showGpu;
    [ObservableProperty] private bool _showMemory;
    [ObservableProperty] private bool _showDisk;
    [ObservableProperty] private bool _showNetwork;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuNameTooltip), nameof(GpuNameTooltip), nameof(CompactTooltip))]
    private bool _detailedTooltip;

    [ObservableProperty] private string _cpuName = DisplayFormat.Unavailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuNameTooltip), nameof(CompactTooltip))]
    private string _cpuFullName = DisplayFormat.Unavailable;

    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private UsageLevel _cpuLevel;
    [ObservableProperty] private PercentText _cpuUsageText = DisplayFormat.PercentParts(null);
    [ObservableProperty] private string _cpuFrequencyText = DisplayFormat.Unavailable;

    [ObservableProperty] private double _memoryUsage;
    [ObservableProperty] private UsageLevel _memoryLevel;
    [ObservableProperty] private PercentText _memoryUsageText = DisplayFormat.PercentParts(null);
    [ObservableProperty] private string _memoryAmountText = DisplayFormat.Unavailable;

    [ObservableProperty] private string _gpuName = DisplayFormat.Unavailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GpuNameTooltip), nameof(CompactTooltip))]
    private string _gpuFullName = DisplayFormat.Unavailable;

    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private UsageLevel _gpuLevel;
    [ObservableProperty] private PercentText _gpuUsageText = DisplayFormat.PercentParts(null);
    [ObservableProperty] private string _gpuMemoryText = DisplayFormat.Unavailable;

    [ObservableProperty] private string _diskName = DisplayFormat.Unavailable;
    [ObservableProperty] private double _diskActive;
    [ObservableProperty] private UsageLevel _diskLevel;
    [ObservableProperty] private PercentText _diskActiveText = DisplayFormat.PercentParts(null);
    [ObservableProperty] private IReadOnlyList<DiskDetail> _diskDetails = [];

    [ObservableProperty] private string _downloadSpeedText = DisplayFormat.Unavailable;
    [ObservableProperty] private string _uploadSpeedText = DisplayFormat.Unavailable;
    [ObservableProperty] private string _networkSessionText = DisplayFormat.Unavailable;

    public string? CpuNameTooltip => DetailedTooltip ? null : CpuFullName;

    public string? GpuNameTooltip => DetailedTooltip ? null : GpuFullName;

    public string? CompactTooltip => DetailedTooltip ? null : $"{CpuFullName}{Environment.NewLine}{GpuFullName}";

    public IReadOnlyList<DiskActivity> AvailableDisks => _lastSnapshot?.Disk.Disks ?? [];

    public void UpdateSettings(DisplaySettings display, ColorSettings colors)
    {
        _display = display;
        _colors = colors;
        ApplyDisplay();

        if (_lastSnapshot is not null)
        {
            Apply(_lastSnapshot);
        }
    }

    public void Apply(HardwareSnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        CpuName = DisplayFormat.ShortName(snapshot.Cpu.Name);
        CpuFullName = snapshot.Cpu.Name ?? DisplayFormat.Unavailable;
        CpuUsage = snapshot.Cpu.UsagePercent ?? 0;
        CpuLevel = UsageLevels.Classify(snapshot.Cpu.UsagePercent, _colors.Cpu, _colors.Enabled);
        CpuUsageText = DisplayFormat.PercentParts(snapshot.Cpu.UsagePercent);
        CpuFrequencyText = DisplayFormat.Gigahertz(snapshot.Cpu.FrequencyGhz);

        MemoryUsage = snapshot.Memory?.UsagePercent ?? 0;
        MemoryLevel = UsageLevels.Classify(snapshot.Memory?.UsagePercent, _colors.Memory, _colors.Enabled);
        MemoryUsageText = DisplayFormat.PercentParts(snapshot.Memory?.UsagePercent);
        MemoryAmountText = DisplayFormat.UsedOfTotal(snapshot.Memory?.UsedBytes, snapshot.Memory?.TotalBytes);

        GpuName = DisplayFormat.ShortName(snapshot.Gpu.Name);
        GpuFullName = snapshot.Gpu.Name ?? DisplayFormat.Unavailable;
        GpuUsage = snapshot.Gpu.UsagePercent ?? 0;
        GpuLevel = UsageLevels.Classify(snapshot.Gpu.UsagePercent, _colors.Gpu, _colors.Enabled);
        GpuUsageText = DisplayFormat.PercentParts(snapshot.Gpu.UsagePercent);
        GpuMemoryText = DisplayFormat.UsedOfTotal(snapshot.Gpu.MemoryUsedBytes, snapshot.Gpu.MemoryTotalBytes);

        ApplyDisk(snapshot.Disk);

        DownloadSpeedText = DisplayFormat.MegabitsPerSecond(snapshot.Network.DownloadBytesPerSecond);
        UploadSpeedText = DisplayFormat.MegabitsPerSecond(snapshot.Network.UploadBytesPerSecond);
        NetworkSessionText =
            $"Sessão   ↓ {DisplayFormat.Size(snapshot.Network.SessionReceivedBytes)}   " +
            $"↑ {DisplayFormat.Size(snapshot.Network.SessionSentBytes)}";
    }

    private void ApplyDisplay()
    {
        ShowCpu = _display.ShowCpu;
        ShowGpu = _display.ShowGpu;
        ShowMemory = _display.ShowMemory;
        ShowDisk = _display.ShowDisk;
        ShowNetwork = _display.ShowNetwork;
        DetailedTooltip = _display.DetailedTooltip;
    }

    private void ApplyDisk(DiskSnapshot disks)
    {
        DiskActivity? disk = _display.DiskNumber is int number ? disks.Find(number) : disks.Busiest;

        DiskName = disk is not null
            ? DisplayFormat.DiskLabel(disk)
            : _display.DiskNumber is int missing ? $"{missing} (não encontrado)" : DisplayFormat.Unavailable;
        DiskActive = disk?.ActivePercent ?? 0;
        DiskLevel = UsageLevels.Classify(disk?.ActivePercent, _colors.Disk, _colors.Enabled);
        DiskActiveText = DisplayFormat.PercentParts(disk?.ActivePercent);
        DiskDetails = disks.Disks
            .Select(item => new DiskDetail($"Disco {DisplayFormat.DiskLabel(item)}", DisplayFormat.Percent(item.ActivePercent)))
            .ToList();
    }
}

public sealed record DiskDetail(string Name, string ActiveText);
