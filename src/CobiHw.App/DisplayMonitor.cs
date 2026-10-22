namespace CobiHw.App;

/// <summary>One screen as Windows reports it.</summary>
/// <param name="Device">Device name used by Windows.</param>
/// <param name="Left">Left edge in pixels.</param>
/// <param name="Top">Top edge in pixels.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="IsPrimary">True for the main screen.</param>
public sealed record ScreenInfo(string Device, int Left, int Top, int Width, int Height, bool IsPrimary);

/// <summary>A monitor as listed in the settings window.</summary>
/// <param name="Device">Device name used by Windows.</param>
/// <param name="Number">Position from left to right, starting at 1.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="IsPrimary">True for the main screen.</param>
public sealed record DisplayMonitor(string Device, int Number, int Width, int Height, bool IsPrimary)
{
    /// <summary>Numbers the screens from left to right, then top to bottom.</summary>
    /// <param name="screens">Screens reported by Windows.</param>
    public static List<DisplayMonitor> ByPosition(IEnumerable<ScreenInfo> screens) =>
    [
        .. screens
            .OrderBy(screen => screen.Left)
            .ThenBy(screen => screen.Top)
            .Select((screen, index) => new DisplayMonitor(screen.Device, index + 1, screen.Width, screen.Height, screen.IsPrimary)),
    ];
}
