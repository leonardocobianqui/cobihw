namespace CobiHw.App.Settings;

public enum DiskSource
{
    System,
    Specific,
}

public sealed record DisplaySettings
{
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowMemory { get; set; } = true;
    public bool ShowDisk { get; set; } = true;
    public bool ShowNetwork { get; set; } = true;
    public DiskSource DiskSource { get; set; } = DiskSource.System;
    public int? DiskNumber { get; set; }
    public bool DetailedTooltip { get; set; } = true;
    public MetricOrder Order { get; set; } = MetricOrder.Default;

    public bool IsShown(MetricItem item) => item switch
    {
        MetricItem.Cpu => ShowCpu,
        MetricItem.Gpu => ShowGpu,
        MetricItem.Memory => ShowMemory,
        MetricItem.Disk => ShowDisk,
        MetricItem.Network => ShowNetwork,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };
}
