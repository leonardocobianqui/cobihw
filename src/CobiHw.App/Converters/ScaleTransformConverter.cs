using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CobiHw.App.Converters;

/// <summary>Turns the zoom into a ScaleTransform for LayoutTransform.</summary>
public sealed class ScaleTransformConverter : IValueConverter
{
    /// <summary>Shared instance used from XAML.</summary>
    public static ScaleTransformConverter Instance { get; } = new();

    /// <summary>Creates the transform, frozen so WPF can share it and skip change tracking.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double scale = (double)value;
        var transform = new ScaleTransform(scale, scale);
        transform.Freeze();
        return transform;
    }

    /// <summary>Not supported.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("The scale is display-only; there is no conversion back.");
}
