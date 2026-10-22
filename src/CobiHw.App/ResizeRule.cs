using CobiHw.App.Settings;

namespace CobiHw.App;

/// <summary>Edge or corner grabbed to resize the widget.</summary>
[Flags]
public enum ResizeEdge
{
    /// <summary>Not on an edge.</summary>
    None = 0,
    /// <summary>Left edge.</summary>
    Left = 1,
    /// <summary>Top edge.</summary>
    Top = 2,
    /// <summary>Right edge.</summary>
    Right = 4,
    /// <summary>Bottom edge.</summary>
    Bottom = 8,
    /// <summary>Top-left corner.</summary>
    TopLeft = Top | Left,
    /// <summary>Top-right corner.</summary>
    TopRight = Top | Right,
    /// <summary>Bottom-left corner.</summary>
    BottomLeft = Bottom | Left,
    /// <summary>Bottom-right corner.</summary>
    BottomRight = Bottom | Right,
}

/// <summary>Math for resizing the widget by dragging an edge; the drag changes the zoom, not the window size.</summary>
public static class ResizeRule
{
    // On a small widget the grab area shrinks, so there is still room to move it.
    private const double MaximumBorderShare = 0.2;

    /// <summary>Keeps a zoom percentage within the allowed range.</summary>
    /// <param name="percent">Requested zoom.</param>
    public static int ClampPercent(int percent) =>
        Math.Clamp(percent, AppearanceSettings.MinimumScale, AppearanceSettings.MaximumScale);

    /// <summary>Edge or corner under the mouse.</summary>
    /// <param name="x">Mouse X inside the window, in DIPs.</param>
    /// <param name="y">Mouse Y inside the window, in DIPs.</param>
    /// <param name="width">Window width.</param>
    /// <param name="height">Window height.</param>
    /// <param name="border">Width of the grab area.</param>
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

    /// <summary>New zoom for a drag: follows the direction that moved most, and never grows past the work area.</summary>
    /// <param name="edge">Edge being dragged.</param>
    /// <param name="startPercent">Zoom when the drag started.</param>
    /// <param name="start">Window when the drag started.</param>
    /// <param name="deltaX">Horizontal mouse movement in pixels.</param>
    /// <param name="deltaY">Vertical mouse movement in pixels.</param>
    /// <param name="workArea">Work area of the monitor.</param>
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

    /// <summary>Where the window goes after a resize, so the edge opposite the dragged one stays in place.</summary>
    /// <param name="edge">Edge being dragged.</param>
    /// <param name="start">Window when the drag started.</param>
    /// <param name="width">New width.</param>
    /// <param name="height">New height.</param>
    public static PixelRect Anchor(ResizeEdge edge, PixelRect start, int width, int height)
    {
        int left = edge.HasFlag(ResizeEdge.Left) ? start.Right - width : start.Left;
        int top = edge.HasFlag(ResizeEdge.Top) ? start.Bottom - height : start.Top;
        return new PixelRect(left, top, left + width, top + height);
    }
}
