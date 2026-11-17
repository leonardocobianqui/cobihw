using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CobiHw.App.Linux;

/// <summary>
/// Turns the view model's plain values into Avalonia types. The view models are shared with the Windows app, so they
/// hold colors as "#AARRGGBB" text and sizes as numbers.
/// </summary>
public static class Converters
{
    /// <summary>"#AARRGGBB" or "#RRGGBB" to a brush.</summary>
    public static FuncValueConverter<string?, IBrush?> ToBrush { get; } = new(hex => Color.TryParse(hex, out Color color) ? new SolidColorBrush(color) : null);

    /// <summary>"#AARRGGBB" or "#RRGGBB" to a color, for effects.</summary>
    public static FuncValueConverter<string?, Color> ToColor { get; } = new(hex => Color.TryParse(hex, out Color color) ? color : Colors.Transparent);

    /// <summary>A weight name from the settings ("Normal", "SemiBold"…) to a font weight.</summary>
    public static FuncValueConverter<string?, FontWeight> ToWeight { get; } = new(name => Enum.TryParse(name, out FontWeight weight) ? weight : FontWeight.Normal);

    /// <summary>The zoom (1 = 100%) to a transform for LayoutTransformControl.</summary>
    public static FuncValueConverter<double, ITransform> ToScale { get; } = new(scale => new ScaleTransform(scale, scale));

    /// <summary>A radius to the same radius on every corner.</summary>
    public static FuncValueConverter<double, CornerRadius> ToCorners { get; } = new(radius => new CornerRadius(radius));

    /// <summary>A thickness to the same thickness on every side.</summary>
    public static FuncValueConverter<double, Thickness> ToSides { get; } = new(width => new Thickness(width));
}
