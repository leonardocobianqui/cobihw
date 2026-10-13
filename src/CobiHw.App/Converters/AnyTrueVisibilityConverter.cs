using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CobiHw.App.Converters;

public sealed class AnyTrueVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Any(value => value is true) ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("A visibilidade é só de exibição; não há conversão de volta.");
}
