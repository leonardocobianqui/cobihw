using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Resources;

namespace CobiHw.App.Settings;

/// <summary>One draggable row in the Display tab's item list.</summary>
public sealed partial class MetricRowViewModel : ObservableObject
{
    public MetricRowViewModel(MetricItem item) => Item = item;

    /// <summary>Item this row controls.</summary>
    public MetricItem Item { get; }

    /// <summary>Translated item name.</summary>
    public string Name => NameOf(Item);

    // Checkbox: show the item in the widget.
    [ObservableProperty] private bool _isVisible;

    // False hides the row (e.g. Battery on a desktop).
    [ObservableProperty] private bool _isAvailable = true;

    // True while the row is being dragged, for the visual feedback.
    [ObservableProperty] private bool _isDragging;

    /// <summary>Translated name of an item.</summary>
    public static string NameOf(MetricItem item) => item switch
    {
        MetricItem.Cpu => Strings.Metric_Cpu,
        MetricItem.Gpu => Strings.Metric_Gpu,
        MetricItem.Memory => Strings.Metric_Memory,
        MetricItem.Disk => Strings.Metric_Disk,
        MetricItem.Network => Strings.Metric_Network,
        MetricItem.Battery => Strings.Metric_Battery,
        MetricItem.Fps => Strings.Metric_Fps,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };

    /// <summary>Updates the name after a language change.</summary>
    public void RefreshName() => OnPropertyChanged(nameof(Name));
}
