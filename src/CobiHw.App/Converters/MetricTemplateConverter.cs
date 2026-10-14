using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CobiHw.App.Settings;

namespace CobiHw.App.Converters;

public sealed class MetricTemplateConverter : IValueConverter
{
    public DataTemplate? Cpu { get; set; }
    public DataTemplate? Gpu { get; set; }
    public DataTemplate? Memory { get; set; }
    public DataTemplate? Disk { get; set; }
    public DataTemplate? Network { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value switch
        {
            MetricItem.Cpu => Cpu,
            MetricItem.Gpu => Gpu,
            MetricItem.Memory => Memory,
            MetricItem.Disk => Disk,
            MetricItem.Network => Network,
            _ => null,
        }) ?? DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("O modelo é só de exibição; não há conversão de volta.");
}
