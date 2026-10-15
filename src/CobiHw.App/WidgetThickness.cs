using System.Windows;

namespace CobiHw.App;

public static class WidgetThickness
{
    public static Thickness Full { get; } = Of(CornerRule.Full);
    public static Thickness CompactHorizontal { get; } = Of(CornerRule.CompactHorizontal);
    public static Thickness CompactVertical { get; } = Of(CornerRule.CompactVertical);
    public static Thickness Details { get; } = Of(CornerRule.Details);

    private static Thickness Of(Inset inset) => new(inset.Horizontal, inset.Vertical, inset.Horizontal, inset.Vertical);
}
