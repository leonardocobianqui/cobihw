using System.Text.Json.Serialization;
using CobiHw.Core;

namespace CobiHw.App.Settings;

/// <summary>Which disk the widget shows.</summary>
public enum DiskSource
{
    /// <summary>The disk that holds Windows.</summary>
    System,
    /// <summary>A disk chosen by number.</summary>
    Specific,
}

/// <summary>Display tab: which items show, their order and the disk/GPU choices.</summary>
public sealed record DisplaySettings
{
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowMemory { get; set; } = true;
    public bool ShowDisk { get; set; } = true;
    public bool ShowNetwork { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    /// <summary>FPS item; needs administrator rights, so it starts off.</summary>
    public bool ShowFps { get; set; }
    public DiskSource DiskSource { get; set; } = DiskSource.System;
    /// <summary>Disk number when <see cref="DiskSource"/> is <see cref="DiskSource.Specific"/>.</summary>
    public int? DiskNumber { get; set; }
    /// <summary>Read/write speed next to the disk.</summary>
    public bool ShowDiskSpeed { get; set; }
    /// <summary>Chosen GPU; null = automatic (Windows' preferred GPU).</summary>
    public GpuChoice? Gpu { get; set; }
    /// <summary>Temperature next to the GPU (always shown in the details).</summary>
    public bool ShowGpuTemperature { get; set; }
    /// <summary>Show the details popup when hovering the widget.</summary>
    public bool DetailedTooltip { get; set; } = true;
    /// <summary>Order of the items in the widget.</summary>
    public MetricOrder Order { get; set; } = MetricOrder.Default;

    /// <summary>Whether the user turned an item on.</summary>
    public bool IsShown(MetricItem item) => item switch
    {
        MetricItem.Cpu => ShowCpu,
        MetricItem.Gpu => ShowGpu,
        MetricItem.Memory => ShowMemory,
        MetricItem.Disk => ShowDisk,
        MetricItem.Network => ShowNetwork,
        MetricItem.Battery => ShowBattery,
        MetricItem.Fps => ShowFps,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };
}

/// <summary>Saved GPU choice; the name keeps showing even if the GPU is gone.</summary>
/// <param name="VendorId">PCI vendor id.</param>
/// <param name="DeviceId">PCI device id.</param>
/// <param name="Instance">0, 1… for identical cards.</param>
/// <param name="Name">Name at the time it was chosen.</param>
public sealed record GpuChoice(uint VendorId, uint DeviceId, int Instance, string Name)
{
    /// <summary>Identity to look the GPU up in a snapshot; not saved.</summary>
    [JsonIgnore]
    public GpuId Id => new(VendorId, DeviceId, Instance);

    /// <summary>Choice for a GPU from a snapshot.</summary>
    public static GpuChoice Of(GpuActivity gpu) => new(gpu.Id.VendorId, gpu.Id.DeviceId, gpu.Id.Instance, gpu.Name);
}
