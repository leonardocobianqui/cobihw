using System.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using CobiHw.Presentation.Settings;

namespace CobiHw.App.Linux;

/// <summary>
/// Item template of a layout: picks the template of each metric and puts a divider before every item but the first.
/// </summary>
/// <remarks>
/// The list holds <see cref="MetricItem"/> values, but every template shows the view model, as in the Windows app: each
/// item takes the DataContext of its ItemsControl once it's in the tree.
/// </remarks>
public sealed class MetricTemplates : IDataTemplate
{
    public IDataTemplate? Cpu { get; set; }
    public IDataTemplate? Gpu { get; set; }
    public IDataTemplate? Memory { get; set; }
    public IDataTemplate? Disk { get; set; }
    public IDataTemplate? Network { get; set; }
    public IDataTemplate? Battery { get; set; }
    public IDataTemplate? Fps { get; set; }

    /// <summary>Style class of the divider, which sets its size and color; empty for no divider.</summary>
    public string Divider { get; set; } = string.Empty;

    /// <summary>Lays the divider and the item side by side instead of stacked.</summary>
    public bool Horizontal { get; set; }

    public bool Match(object? data) => data is MetricItem;

    public Control? Build(object? param)
    {
        if (param is not MetricItem item || Template(item)?.Build(null) is not Control content)
        {
            return null;
        }

        var panel = new StackPanel { Orientation = Horizontal ? Avalonia.Layout.Orientation.Horizontal : Avalonia.Layout.Orientation.Vertical };
        Border? divider = null;
        if (Divider.Length > 0)
        {
            divider = new Border();
            divider.Classes.Add(Divider);
            panel.Children.Add(divider);
        }

        panel.Children.Add(content);
        panel.AttachedToVisualTree += (_, _) =>
        {
            ItemsControl? list = panel.FindAncestorOfType<ItemsControl>();
            panel.DataContext = list?.DataContext;
            if (divider is not null)
            {
                divider.IsVisible = list?.ItemsSource is IEnumerable items && !Equals(items.Cast<object>().FirstOrDefault(), item);
            }
        };

        return panel;
    }

    private IDataTemplate? Template(MetricItem item) => item switch
    {
        MetricItem.Cpu => Cpu,
        MetricItem.Gpu => Gpu,
        MetricItem.Memory => Memory,
        MetricItem.Disk => Disk,
        MetricItem.Network => Network,
        MetricItem.Battery => Battery,
        MetricItem.Fps => Fps,
        _ => null,
    };
}
