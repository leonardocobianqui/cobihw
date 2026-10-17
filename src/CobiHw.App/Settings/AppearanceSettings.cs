namespace CobiHw.App.Settings;

public enum TextWeight
{
    Light,
    Normal,
    Medium,
    SemiBold,
    Bold,
}

public enum BorderMode
{
    Off,
    Always,
    Hover,
}

public sealed record AppearanceSettings
{
    public const int MaximumTransparency = 100;
    public const string DefaultBackgroundColor = "#1E1F22";
    public const string DefaultTextColor = "#E6E6E6";
    public const int MinimumScale = 50;
    public const int MaximumScale = 200;
    public const int DefaultScale = 100;
    public const int MinimumBorderThickness = 1;
    public const int MaximumBorderThickness = 6;
    public const int DefaultBorderTransparency = 50;
    public const int MaximumBarRadius = 3;

    public string BackgroundColor { get; set; } = DefaultBackgroundColor;
    public int Transparency { get; set; }
    public string TextColor { get; set; } = DefaultTextColor;
    public TextWeight TextWeight { get; set; } = TextWeight.Normal;
    public int Scale { get; set; } = DefaultScale;
    public BorderMode BorderMode { get; set; } = BorderMode.Off;
    public string BorderColor { get; set; } = DefaultTextColor;
    public int BorderThickness { get; set; } = MinimumBorderThickness;
    public int BorderTransparency { get; set; } = DefaultBorderTransparency;
    public int CornerRadius { get; set; }
    public int BarRadius { get; set; }
}
