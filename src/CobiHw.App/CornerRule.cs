namespace CobiHw.App;

public readonly record struct Inset(double Horizontal, double Vertical);

public static class CornerRule
{
    public static readonly Inset Full = new(16, 14);
    public static readonly Inset CompactHorizontal = new(14, 8);
    public static readonly Inset CompactVertical = new(12, 10);
    public static readonly Inset Details = new(10, 8);

    public static int MaximumRadius { get; } = (int)Math.Floor(new[] { Full, CompactHorizontal, CompactVertical, Details }.Min(LargestRadius));

    public static double LargestRadius(Inset inset) =>
        inset.Horizontal + inset.Vertical + Math.Sqrt(2 * inset.Horizontal * inset.Vertical);

    public static int Clamp(int radius) => Math.Clamp(radius, 0, MaximumRadius);
}
