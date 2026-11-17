using System.Globalization;
using Avalonia.Data.Converters;

namespace CobiHw.App.Linux;

/// <summary>True when the bound layout is the one named in ConverterParameter: only the panel of the current layout is shown.</summary>
public sealed class LayoutIs : IValueConverter
{
    /// <summary>Shared instance used from XAML.</summary>
    public static LayoutIs Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && value.ToString() == parameter as string;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("The layout is display-only; there is no conversion back.");
}
