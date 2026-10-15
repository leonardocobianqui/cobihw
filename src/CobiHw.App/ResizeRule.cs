using CobiHw.App.Settings;

namespace CobiHw.App;

[Flags]
public enum ResizeEdge
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
    TopLeft = Top | Left,
    TopRight = Top | Right,
    BottomLeft = Bottom | Left,
    BottomRight = Bottom | Right,
}

public static class ResizeRule
{
    private const double MaximumBorderShare = 0.2;

    public static int ClampPercent(int percent) =>
        Math.Clamp(percent, AppearanceSettings.MinimumScale, AppearanceSettings.MaximumScale);

    public static ResizeEdge HitTest(double x, double y, double width, double height, double border)
    {
        if (x < 0 || y < 0 || x > width || y > height)
        {
            return ResizeEdge.None;
        }

        double horizontalBorder = Math.Min(border, width * MaximumBorderShare);
        double verticalBorder = Math.Min(border, height * MaximumBorderShare);
        ResizeEdge edge = ResizeEdge.None;

        if (x < horizontalBorder)
        {
            edge |= ResizeEdge.Left;
        }
        else if (x >= width - horizontalBorder)
        {
            edge |= ResizeEdge.Right;
        }

        if (y < verticalBorder)
        {
            edge |= ResizeEdge.Top;
        }
        else if (y >= height - verticalBorder)
        {
            edge |= ResizeEdge.Bottom;
        }

        return edge;
    }

    public static int PercentFor(ResizeEdge edge, int startPercent, PixelRect start, int deltaX, int deltaY, PixelRect workArea)
    {
        double horizontal = edge.HasFlag(ResizeEdge.Right) ? deltaX : edge.HasFlag(ResizeEdge.Left) ? -deltaX : 0;
        double vertical = edge.HasFlag(ResizeEdge.Bottom) ? deltaY : edge.HasFlag(ResizeEdge.Top) ? -deltaY : 0;
        double widthFactor = (start.Width + horizontal) / start.Width;
        double heightFactor = (start.Height + vertical) / start.Height;
        double factor = Math.Abs(widthFactor - 1) >= Math.Abs(heightFactor - 1) ? widthFactor : heightFactor;

        double fitsScreen = Math.Min((double)workArea.Width / start.Width, (double)workArea.Height / start.Height);
        int maximum = Math.Min(AppearanceSettings.MaximumScale, (int)Math.Floor(startPercent * fitsScreen));
        int percent = (int)Math.Round(startPercent * factor);

        return Math.Max(AppearanceSettings.MinimumScale, Math.Min(percent, maximum));
    }

    public static PixelRect Anchor(ResizeEdge edge, PixelRect start, int width, int height)
    {
        int left = edge.HasFlag(ResizeEdge.Left) ? start.Right - width : start.Left;
        int top = edge.HasFlag(ResizeEdge.Top) ? start.Bottom - height : start.Top;
        return new PixelRect(left, top, left + width, top + height);
    }
}
