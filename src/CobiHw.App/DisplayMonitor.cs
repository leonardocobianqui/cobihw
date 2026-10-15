namespace CobiHw.App;

public sealed record ScreenInfo(string Device, int Left, int Top, int Width, int Height, bool IsPrimary);

public sealed record DisplayMonitor(string Device, int Number, int Width, int Height, bool IsPrimary)
{
    public static List<DisplayMonitor> ByPosition(IEnumerable<ScreenInfo> screens) =>
    [
        .. screens
            .OrderBy(screen => screen.Left)
            .ThenBy(screen => screen.Top)
            .Select((screen, index) => new DisplayMonitor(screen.Device, index + 1, screen.Width, screen.Height, screen.IsPrimary)),
    ];
}
