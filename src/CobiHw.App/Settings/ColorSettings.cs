namespace CobiHw.App.Settings;

public sealed record UsageThresholds(int Warning, int Critical)
{
    public const int Minimum = 1;
    public const int Maximum = 100;

    public static UsageThresholds Default { get; } = new(50, 75);
}

public sealed record ColorSettings
{
    public const string DefaultNormalColor = "#6FD68A";
    public const string DefaultWarningColor = "#F0C44C";
    public const string DefaultCriticalColor = "#FF6B63";

    public bool Enabled { get; set; } = true;
    public string NormalColor { get; set; } = DefaultNormalColor;
    public string WarningColor { get; set; } = DefaultWarningColor;
    public string CriticalColor { get; set; } = DefaultCriticalColor;
    public UsageThresholds Cpu { get; set; } = UsageThresholds.Default;
    public UsageThresholds Gpu { get; set; } = UsageThresholds.Default;
    public UsageThresholds Memory { get; set; } = UsageThresholds.Default;
    public UsageThresholds Disk { get; set; } = UsageThresholds.Default;
}
