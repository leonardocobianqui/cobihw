using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Resources;

namespace CobiHw.App.Settings;

public sealed partial class MetricRowViewModel : ObservableObject
{
    public MetricRowViewModel(MetricItem item) => Item = item;

    public MetricItem Item { get; }

    public string Name => NameOf(Item);

    [ObservableProperty] private bool _isVisible;

    [ObservableProperty] private bool _isDragging;

    public static string NameOf(MetricItem item) => item switch
    {
        MetricItem.Cpu => Strings.Metric_Cpu,
        MetricItem.Gpu => Strings.Metric_Gpu,
        MetricItem.Memory => Strings.Metric_Memory,
        MetricItem.Disk => Strings.Metric_Disk,
        MetricItem.Network => Strings.Metric_Network,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };

    public void RefreshName() => OnPropertyChanged(nameof(Name));
}
