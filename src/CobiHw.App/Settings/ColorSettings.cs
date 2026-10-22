namespace CobiHw.App.Settings;

/// <summary>Usage percentages where the color changes.</summary>
/// <param name="Warning">From this value on, the warning color.</param>
/// <param name="Critical">From this value on, the critical color; must be above <paramref name="Warning"/>.</param>
public sealed record UsageThresholds(int Warning, int Critical)
{
    public const int Minimum = 1;
    public const int Maximum = 100;

    /// <summary>50% warning, 75% critical.</summary>
    public static UsageThresholds Default { get; } = new(50, 75);
}

/// <summary>Usage colors: whether they are on, the three colors and each item's thresholds.</summary>
public sealed record ColorSettings
{
    public const string DefaultNormalColor = "#6FD68A";
    public const string DefaultWarningColor = "#F0C44C";
    public const string DefaultCriticalColor = "#FF6B63";

    /// <summary>Off = bars and percentages use the accent and text colors.</summary>
    public bool Enabled { get; set; } = true;
    public string NormalColor { get; set; } = DefaultNormalColor;
    public string WarningColor { get; set; } = DefaultWarningColor;
    public string CriticalColor { get; set; } = DefaultCriticalColor;
    public UsageThresholds Cpu { get; set; } = UsageThresholds.Default;
    public UsageThresholds Gpu { get; set; } = UsageThresholds.Default;
    public UsageThresholds Memory { get; set; } = UsageThresholds.Default;
    public UsageThresholds Disk { get; set; } = UsageThresholds.Default;
}
