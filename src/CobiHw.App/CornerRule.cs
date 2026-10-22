namespace CobiHw.App;

/// <summary>Space between the widget edge and its content.</summary>
/// <param name="Horizontal">Left and right space, in DIPs.</param>
/// <param name="Vertical">Top and bottom space, in DIPs.</param>
public readonly record struct Inset(double Horizontal, double Vertical);

/// <summary>Limits the corner radius so rounded corners never cut into the content.</summary>
public static class CornerRule
{
    /// <summary>Inner spacing of the full layout.</summary>
    public static readonly Inset Full = new(16, 14);
    /// <summary>Inner spacing of the compact horizontal layout.</summary>
    public static readonly Inset CompactHorizontal = new(14, 8);
    /// <summary>Inner spacing of the compact vertical layout.</summary>
    public static readonly Inset CompactVertical = new(12, 10);
    /// <summary>Inner spacing of the details tooltip.</summary>
    public static readonly Inset Details = new(10, 8);

    /// <summary>Largest radius that fits every layout.</summary>
    public static int MaximumRadius { get; } = (int)Math.Floor(new[] { Full, CompactHorizontal, CompactVertical, Details }.Min(LargestRadius));

    /// <summary>Largest radius whose curve still passes outside the content corner.</summary>
    /// <remarks>Solves (r − h)² + (r − v)² = r²: the corner circle touches the content corner at (h, v).</remarks>
    /// <param name="inset">Spacing of the layout.</param>
    public static double LargestRadius(Inset inset) =>
        inset.Horizontal + inset.Vertical + Math.Sqrt(2 * inset.Horizontal * inset.Vertical);

    /// <summary>Keeps a radius between 0 and <see cref="MaximumRadius"/>.</summary>
    /// <param name="radius">Requested radius.</param>
    public static int Clamp(int radius) => Math.Clamp(radius, 0, MaximumRadius);
}
