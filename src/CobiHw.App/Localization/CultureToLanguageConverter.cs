using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace CobiHw.App.Localization;

public sealed class CultureToLanguageConverter : IValueConverter
{
    public static CultureToLanguageConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        XmlLanguage.GetLanguage(((CultureInfo)value).IetfLanguageTag);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("O idioma da janela é só de exibição; não há conversão de volta.");
}
