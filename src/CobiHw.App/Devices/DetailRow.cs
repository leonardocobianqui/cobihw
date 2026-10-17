using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;

namespace CobiHw.App.Devices;

public sealed partial class DetailRow(string labelKey) : ObservableObject
{
    public string Label => Localizer.Instance[labelKey];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValue))]
    private string _value = string.Empty;

    public bool HasValue => Value.Length > 0;

    public void RefreshLabel() => OnPropertyChanged(nameof(Label));
}
