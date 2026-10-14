namespace CobiHw.App.Settings;

public enum TextWeight
{
    Light,
    Normal,
    Medium,
    SemiBold,
    Bold,
}

public sealed record AppearanceSettings
{
    public const int MaximumTransparency = 100;
    public const string DefaultBackgroundColor = "#1E1F22";
    public const string DefaultTextColor = "#E6E6E6";

    public string BackgroundColor { get; set; } = DefaultBackgroundColor;
    public int Transparency { get; set; }
    public string TextColor { get; set; } = DefaultTextColor;
    public TextWeight TextWeight { get; set; } = TextWeight.Normal;
}
