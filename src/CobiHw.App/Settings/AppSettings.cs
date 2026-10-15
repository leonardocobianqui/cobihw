using CobiHw.App.Localization;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.App.Settings;

public enum WidgetMode
{
    AlwaysOnTop,
    Desktop,
}

public enum InteractionKey
{
    None,
    Ctrl,
    Shift,
    Alt,
    CtrlShift,
    CtrlAlt,
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public enum ViewLayout
{
    Full,
    CompactHorizontal,
    CompactVertical,
}

public sealed class AppSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public WidgetMode Mode { get; set; } = WidgetMode.AlwaysOnTop;
    public ViewLayout Layout { get; set; } = ViewLayout.Full;
    public bool SnapToEdges { get; set; } = true;
    public bool PositionLocked { get; set; }
    public double? SettingsWidth { get; set; }
    public double? SettingsHeight { get; set; }
    public bool ClickThrough { get; set; }
    public InteractionKey InteractionKey { get; set; } = InteractionKey.Ctrl;
    public string? FpsTaskPath { get; set; }
    public DisplaySettings Display { get; set; } = new();
    public ColorSettings Colors { get; set; } = new();
    public NetworkSettings Network { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public string Language { get; set; } = Localizer.AutomaticLanguage;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public NetworkSessionState? NetworkSession { get; set; }
    public SpeedTestResult? LastSpeedTest { get; set; }
}
