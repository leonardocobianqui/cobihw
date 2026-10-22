namespace CobiHw.App;

/// <summary>Rectangle in physical pixels.</summary>
/// <param name="Left">Left edge.</param>
/// <param name="Top">Top edge.</param>
/// <param name="Right">Right edge (exclusive).</param>
/// <param name="Bottom">Bottom edge (exclusive).</param>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Width in pixels.</summary>
    public int Width => Right - Left;

    /// <summary>Height in pixels.</summary>
    public int Height => Bottom - Top;
}

/// <summary>Placement math that keeps the widget on screen.</summary>
public static class WindowFit
{
    /// <summary>Position after a size change: a widget touching the right or bottom edge stays there, and it never leaves the work area.</summary>
    /// <param name="current">Window before the change.</param>
    /// <param name="requested">Window Windows proposes.</param>
    /// <param name="workArea">Work area of the monitor.</param>
    public static PixelRect Place(PixelRect current, PixelRect requested, PixelRect workArea)
    {
        int x = requested.Left;
        int y = requested.Top;

        if (current.Right == workArea.Right && current.Left != workArea.Left)
        {
            x = workArea.Right - requested.Width;
        }

        if (current.Bottom == workArea.Bottom && current.Top != workArea.Top)
        {
            y = workArea.Bottom - requested.Height;
        }

        x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - requested.Width));
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - requested.Height));

        return new PixelRect(x, y, x + requested.Width, y + requested.Height);
    }

    /// <summary>True when the window is fully inside the work area.</summary>
    /// <param name="window">Window.</param>
    /// <param name="workArea">Work area.</param>
    public static bool IsInside(PixelRect window, PixelRect workArea) =>
        window.Left >= workArea.Left && window.Top >= workArea.Top && window.Right <= workArea.Right && window.Bottom <= workArea.Bottom;

    /// <summary>Moves the window as little as needed to fit inside the work area.</summary>
    /// <param name="window">Window.</param>
    /// <param name="workArea">Work area.</param>
    public static PixelRect BringInside(PixelRect window, PixelRect workArea)
    {
        int x = Math.Max(workArea.Left, Math.Min(window.Left, workArea.Right - window.Width));
        int y = Math.Max(workArea.Top, Math.Min(window.Top, workArea.Bottom - window.Height));
        return new PixelRect(x, y, x + window.Width, y + window.Height);
    }

    /// <summary>Snaps the window to work-area edges closer than the threshold.</summary>
    /// <param name="window">Free position.</param>
    /// <param name="workArea">Work area.</param>
    /// <param name="threshold">Snap distance in pixels.</param>
    public static PixelRect Snap(PixelRect window, PixelRect workArea, int threshold)
    {
        int x = SnapAxis(window.Left, window.Right, workArea.Left, workArea.Right, threshold);
        int y = SnapAxis(window.Top, window.Bottom, workArea.Top, workArea.Bottom, threshold);
        return new PixelRect(x, y, x + window.Width, y + window.Height);
    }

    /// <summary>Moves the window to another work area, keeping its relative position (bottom-right stays bottom-right).</summary>
    /// <param name="window">Window.</param>
    /// <param name="from">Current work area.</param>
    /// <param name="to">Target work area.</param>
    public static PixelRect MoveTo(PixelRect window, PixelRect from, PixelRect to)
    {
        int x = to.Left + Proportional(window.Left - from.Left, from.Width - window.Width, to.Width - window.Width);
        int y = to.Top + Proportional(window.Top - from.Top, from.Height - window.Height, to.Height - window.Height);

        x = Math.Max(to.Left, Math.Min(x, to.Right - window.Width));
        y = Math.Max(to.Top, Math.Min(y, to.Bottom - window.Height));

        return new PixelRect(x, y, x + window.Width, y + window.Height);
    }

    // When both edges are close, the start edge wins.
    private static int SnapAxis(int start, int end, int areaStart, int areaEnd, int threshold)
    {
        int toStart = Math.Abs(start - areaStart);
        int toEnd = Math.Abs(end - areaEnd);

        if (toStart <= threshold && toStart <= toEnd)
        {
            return areaStart;
        }

        if (toEnd <= threshold)
        {
            return areaEnd - (end - start);
        }

        return start;
    }

    // Scales the offset by the free space on each monitor.
    private static int Proportional(int offset, int fromFree, int toFree) =>
        fromFree <= 0 || toFree <= 0 ? 0 : (int)Math.Round((double)offset * toFree / fromFree);
}
