namespace CobiHw.App;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

public static class WindowFit
{
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

    public static PixelRect Snap(PixelRect window, PixelRect workArea, int threshold)
    {
        int x = SnapAxis(window.Left, window.Right, workArea.Left, workArea.Right, threshold);
        int y = SnapAxis(window.Top, window.Bottom, workArea.Top, workArea.Bottom, threshold);
        return new PixelRect(x, y, x + window.Width, y + window.Height);
    }

    public static PixelRect MoveTo(PixelRect window, PixelRect from, PixelRect to)
    {
        int x = to.Left + Proportional(window.Left - from.Left, from.Width - window.Width, to.Width - window.Width);
        int y = to.Top + Proportional(window.Top - from.Top, from.Height - window.Height, to.Height - window.Height);

        x = Math.Max(to.Left, Math.Min(x, to.Right - window.Width));
        y = Math.Max(to.Top, Math.Min(y, to.Bottom - window.Height));

        return new PixelRect(x, y, x + window.Width, y + window.Height);
    }

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

    private static int Proportional(int offset, int fromFree, int toFree) =>
        fromFree <= 0 || toFree <= 0 ? 0 : (int)Math.Round((double)offset * toFree / fromFree);
}
