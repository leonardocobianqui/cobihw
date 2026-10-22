using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace CobiHw.App.Localization;

/// <summary>
/// Converts the app culture to a WPF <see cref="XmlLanguage"/>, so windows format numbers and dates in the chosen language.
/// </summary>
public sealed class CultureToLanguageConverter : IValueConverter
{
    /// <summary>Shared instance for <c>{x:Static}</c> in XAML.</summary>
    public static CultureToLanguageConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        XmlLanguage.GetLanguage(((CultureInfo)value).IetfLanguageTag);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("The window language is display-only; there is no conversion back.");
}
