using CobiHw.Core;

namespace CobiHw.App.Settings;

public enum WidgetMode
{
    AlwaysOnTop,
    Desktop,
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
    public DisplaySettings Display { get; set; } = new();
    public ColorSettings Colors { get; set; } = new();
    public NetworkSessionState? NetworkSession { get; set; }
}
