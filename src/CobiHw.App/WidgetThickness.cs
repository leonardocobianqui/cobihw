using System.Windows;

namespace CobiHw.App;

/// <summary>Inner spacing of each layout as a <see cref="Thickness"/>, for the XAML.</summary>
public static class WidgetThickness
{
    /// <summary>Full layout.</summary>
    public static Thickness Full { get; } = Of(CornerRule.Full);
    /// <summary>Compact horizontal layout.</summary>
    public static Thickness CompactHorizontal { get; } = Of(CornerRule.CompactHorizontal);
    /// <summary>Compact vertical layout.</summary>
    public static Thickness CompactVertical { get; } = Of(CornerRule.CompactVertical);
    /// <summary>Details tooltip.</summary>
    public static Thickness Details { get; } = Of(CornerRule.Details);

    private static Thickness Of(Inset inset) => new(inset.Horizontal, inset.Vertical, inset.Horizontal, inset.Vertical);
}
