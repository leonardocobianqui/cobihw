namespace CobiHw.App.Settings;

public sealed record DisplaySettings
{
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowMemory { get; set; } = true;
    public bool ShowDisk { get; set; } = true;
    public bool ShowNetwork { get; set; } = true;
    public int? DiskNumber { get; set; }
    public bool DetailedTooltip { get; set; }
}
