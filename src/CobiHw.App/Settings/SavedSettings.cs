using CobiHw.App.Localization;

namespace CobiHw.App.Settings;

/// <summary>Everything the settings window edits, grouped by tab.</summary>
/// <param name="Display">Display tab.</param>
/// <param name="Colors">Usage colors (Appearance tab).</param>
/// <param name="Network">Network tab.</param>
/// <param name="Appearance">Appearance tab.</param>
/// <param name="General">General tab.</param>
public sealed record SavedSettings(DisplaySettings Display, ColorSettings Colors, NetworkSettings Network, AppearanceSettings Appearance, GeneralSettings General);

/// <summary>General tab values. Stored flat in <see cref="AppSettings"/>, grouped here for the window.</summary>
public sealed record GeneralSettings
{
    /// <summary>Language code or "auto".</summary>
    public string Language { get; set; } = Localizer.AutomaticLanguage;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public WidgetMode Mode { get; set; } = WidgetMode.AlwaysOnTop;
    /// <summary>Snap the widget to screen edges while dragging.</summary>
    public bool SnapToEdges { get; set; } = true;
    /// <summary>Prevents moving and resizing the widget.</summary>
    public bool PositionLocked { get; set; }
    /// <summary>Clicks go through the widget to the window behind it.</summary>
    public bool ClickThrough { get; set; }
    /// <summary>Key that makes a click-through widget clickable while held.</summary>
    public InteractionKey InteractionKey { get; set; } = InteractionKey.Ctrl;
    /// <summary>Registered to start at Windows logon.</summary>
    public bool StartWithWindows { get; set; }
    /// <summary>Device name of the chosen monitor (e.g. "\\.\DISPLAY2"); null = primary.</summary>
    public string? Monitor { get; set; }
}
