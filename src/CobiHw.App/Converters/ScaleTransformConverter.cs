using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CobiHw.App.Converters;

public sealed class ScaleTransformConverter : IValueConverter
{
    public static ScaleTransformConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double scale = (double)value;
        var transform = new ScaleTransform(scale, scale);
        transform.Freeze();
        return transform;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("A escala é só de exibição; não há conversão de volta.");
}
