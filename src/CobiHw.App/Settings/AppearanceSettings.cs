namespace CobiHw.App.Settings;

/// <summary>Font weight of the widget texts.</summary>
public enum TextWeight
{
    Light,
    Normal,
    Medium,
    SemiBold,
    Bold,
}

/// <summary>When the widget border is drawn.</summary>
public enum BorderMode
{
    Off,
    Always,
    /// <summary>Only while the mouse is over the widget.</summary>
    Hover,
}

/// <summary>Widget look: colors, transparency, text weight, size, border and corner radius.</summary>
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
    /// <summary>Background transparency, 0 (solid) to 100.</summary>
    public int Transparency { get; set; }
    public string TextColor { get; set; } = DefaultTextColor;
    public TextWeight TextWeight { get; set; } = TextWeight.Normal;
    /// <summary>Widget size in percent, 50 to 200.</summary>
    public int Scale { get; set; } = DefaultScale;
    public BorderMode BorderMode { get; set; } = BorderMode.Off;
    public string BorderColor { get; set; } = DefaultTextColor;
    public int BorderThickness { get; set; } = MinimumBorderThickness;
    /// <summary>Border transparency, 0 to 100.</summary>
    public int BorderTransparency { get; set; } = DefaultBorderTransparency;
    /// <summary>Widget corner radius in pixels.</summary>
    public int CornerRadius { get; set; }
    /// <summary>Usage bar corner radius in pixels, 0 to <see cref="MaximumBarRadius"/>.</summary>
    public int BarRadius { get; set; }
}
