using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;

namespace CobiHw.App.Devices;

/// <summary>One label/value row in the network device details; hidden while the value is empty.</summary>
/// <param name="labelKey">Resource key of the translated label.</param>
public sealed partial class DetailRow(string labelKey) : ObservableObject
{
    /// <summary>Translated label.</summary>
    public string Label => Localizer.Instance[labelKey];

    /// <summary>Value; may span several lines.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValue))]
    private string _value = string.Empty;

    /// <summary>True when there is a value to show.</summary>
    public bool HasValue => Value.Length > 0;

    /// <summary>Re-reads the label after a language change.</summary>
    public void RefreshLabel() => OnPropertyChanged(nameof(Label));
}
