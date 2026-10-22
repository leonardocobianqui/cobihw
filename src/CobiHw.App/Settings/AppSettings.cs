using System.Text.Json.Serialization;
using CobiHw.App.Localization;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.App.Settings;

/// <summary>Where the widget lives.</summary>
public enum WidgetMode
{
    /// <summary>Above other windows.</summary>
    AlwaysOnTop,
    /// <summary>Behind other windows, like a desktop gadget.</summary>
    Desktop,
}

/// <summary>Key that temporarily makes a click-through widget clickable.</summary>
public enum InteractionKey
{
    None,
    Ctrl,
    Shift,
    Alt,
    CtrlShift,
    CtrlAlt,
}

/// <summary>Theme of the settings window and tray menu (the widget has its own colors).</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Widget layout.</summary>
public enum ViewLayout
{
    Full,
    CompactHorizontal,
    CompactVertical,
}

/// <summary>
/// Everything saved for one computer and user, serialized as JSON inside the settings file.
/// </summary>
/// <remarks>New properties need a default that matches the old behavior, so older files keep loading.</remarks>
public sealed class AppSettings
{
    /// <summary>Widget position in physical pixels; null = first run.</summary>
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    /// <summary>Old position format (WPF units); read once from older files and cleared on the next save.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? WindowLeft { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? WindowTop { get; set; }
    public WidgetMode Mode { get; set; } = WidgetMode.AlwaysOnTop;
    public ViewLayout Layout { get; set; } = ViewLayout.Full;
    public bool SnapToEdges { get; set; } = true;
    public bool PositionLocked { get; set; }
    /// <summary>Last size of the settings window.</summary>
    public double? SettingsWidth { get; set; }
    public double? SettingsHeight { get; set; }
    public bool ClickThrough { get; set; }
    public InteractionKey InteractionKey { get; set; } = InteractionKey.Ctrl;
    public DisplaySettings Display { get; set; } = new();
    public ColorSettings Colors { get; set; } = new();
    public NetworkSettings Network { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public string Language { get; set; } = Localizer.AutomaticLanguage;
    public AppTheme Theme { get; set; } = AppTheme.System;
    /// <summary>Network session totals, resumed if the app restarts within the same boot.</summary>
    public NetworkSessionState? NetworkSession { get; set; }
    /// <summary>Last speed test result, shown until a new test runs.</summary>
    public SpeedTestResult? LastSpeedTest { get; set; }

    /// <summary>Settings for a computer or user without a profile, copied from the most recent profile.</summary>
    /// <remarks>Hardware-specific choices (disk, GPU, network devices, FPS task) and the window position start fresh.</remarks>
    public AppSettings ForNewComputer() => new()
    {
        Mode = Mode,
        Layout = Layout,
        SnapToEdges = SnapToEdges,
        PositionLocked = PositionLocked,
        ClickThrough = ClickThrough,
        InteractionKey = InteractionKey,
        Display = Display with { ShowFps = false, DiskSource = DiskSource.System, DiskNumber = null, Gpu = null },
        Colors = Colors,
        Network = new NetworkSettings { PingEnabled = Network.PingEnabled, PingHost = Network.PingHost },
        Appearance = Appearance,
        Language = Language,
        Theme = Theme,
    };
}
