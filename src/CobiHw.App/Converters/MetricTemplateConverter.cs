using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CobiHw.App.Settings;

namespace CobiHw.App.Converters;

/// <summary>Picks the template of each item in a list; one instance per layout, set up in XAML.</summary>
public sealed class MetricTemplateConverter : IValueConverter
{
    /// <summary>Template for the CPU item.</summary>
    public DataTemplate? Cpu { get; set; }
    /// <summary>Template for the GPU item.</summary>
    public DataTemplate? Gpu { get; set; }
    /// <summary>Template for the RAM item.</summary>
    public DataTemplate? Memory { get; set; }
    /// <summary>Template for the disk item.</summary>
    public DataTemplate? Disk { get; set; }
    /// <summary>Template for the network item.</summary>
    public DataTemplate? Network { get; set; }
    /// <summary>Template for the battery item.</summary>
    public DataTemplate? Battery { get; set; }
    /// <summary>Template for the FPS item.</summary>
    public DataTemplate? Fps { get; set; }

    /// <summary>Template of the item, or no value for an unknown item.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value switch
        {
            MetricItem.Cpu => Cpu,
            MetricItem.Gpu => Gpu,
            MetricItem.Memory => Memory,
            MetricItem.Disk => Disk,
            MetricItem.Network => Network,
            MetricItem.Battery => Battery,
            MetricItem.Fps => Fps,
            _ => null,
        }) ?? DependencyProperty.UnsetValue;

    /// <summary>Not supported.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("The template is display-only; there is no conversion back.");
}
