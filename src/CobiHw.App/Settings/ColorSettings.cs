namespace CobiHw.App.Settings;

public sealed record UsageThresholds(int Warning, int Critical)
{
    public const int Minimum = 1;
    public const int Maximum = 100;

    public static UsageThresholds Default { get; } = new(50, 75);
}

public sealed record ColorSettings
{
    public bool Enabled { get; set; } = true;
    public UsageThresholds Cpu { get; set; } = UsageThresholds.Default;
    public UsageThresholds Gpu { get; set; } = UsageThresholds.Default;
    public UsageThresholds Memory { get; set; } = UsageThresholds.Default;
    public UsageThresholds Disk { get; set; } = UsageThresholds.Default;
}
