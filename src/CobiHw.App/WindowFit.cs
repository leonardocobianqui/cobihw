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
}
