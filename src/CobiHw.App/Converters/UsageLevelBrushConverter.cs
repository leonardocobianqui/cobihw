using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CobiHw.App.Converters;

public sealed class UsageLevelBrushConverter : IValueConverter
{
    public Brush Neutral { get; set; } = Brushes.White;
    public Brush Normal { get; set; } = Brushes.Green;
    public Brush Warning { get; set; } = Brushes.Goldenrod;
    public Brush Critical { get; set; } = Brushes.Red;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            UsageLevel.Normal => Normal,
            UsageLevel.Warning => Warning,
            UsageLevel.Critical => Critical,
            _ => Neutral,
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("A cor é só de exibição; não há conversão de volta.");
}
