using CobiHw.App.Localization;

namespace CobiHw.App.Settings;

public sealed record SavedSettings(DisplaySettings Display, ColorSettings Colors, NetworkSettings Network, AppearanceSettings Appearance, GeneralSettings General);

public sealed record GeneralSettings
{
    public string Language { get; set; } = Localizer.AutomaticLanguage;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public WidgetMode Mode { get; set; } = WidgetMode.AlwaysOnTop;
    public bool SnapToEdges { get; set; } = true;
    public bool PositionLocked { get; set; }
    public bool ClickThrough { get; set; }
    public InteractionKey InteractionKey { get; set; } = InteractionKey.Ctrl;
    public bool StartWithWindows { get; set; }
    public string? Monitor { get; set; }
}
