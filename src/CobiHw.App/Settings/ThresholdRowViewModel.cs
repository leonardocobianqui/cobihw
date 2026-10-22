using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;
using CobiHw.App.Resources;

namespace CobiHw.App.Settings;

/// <summary>Warning/critical fields of one item in the usage colors editor, validated as the user types.</summary>
public sealed partial class ThresholdRowViewModel : ObservableObject
{
    private readonly MetricItem _item;

    public ThresholdRowViewModel(MetricItem item, UsageThresholds thresholds)
    {
        _item = item;
        Reset(thresholds);
    }

    /// <summary>Translated item name.</summary>
    public string Name => MetricRowViewModel.NameOf(_item);

    // Kept as text, so invalid input stays on screen with an error instead of being rejected.
    [ObservableProperty] private string _warningText = string.Empty;
    [ObservableProperty] private string _criticalText = string.Empty;

    // Error message, or null when both values are valid.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    /// <summary>True when the values can't be applied.</summary>
    public bool HasError => Error is not null;

    /// <summary>Loads thresholds into the fields.</summary>
    public void Reset(UsageThresholds thresholds)
    {
        WarningText = thresholds.Warning.ToString(Localizer.Instance.Culture);
        CriticalText = thresholds.Critical.ToString(Localizer.Instance.Culture);
    }

    /// <summary>Updates the name and error message after a language change.</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Name));
        Validate();
    }

    /// <summary>The values as thresholds.</summary>
    /// <exception cref="InvalidOperationException">The fields are invalid; check <see cref="HasError"/> first.</exception>
    public UsageThresholds ToThresholds() =>
        TryParse(out UsageThresholds? thresholds, out _)
            ? thresholds
            : throw new InvalidOperationException($"Invalid thresholds for {Name}: {Error}");

    partial void OnWarningTextChanged(string value) => Validate();

    partial void OnCriticalTextChanged(string value) => Validate();

    private void Validate()
    {
        TryParse(out _, out string? error);
        Error = error;
    }

    // Both values must be 1–100 and warning must be below critical.
    private bool TryParse([NotNullWhen(true)] out UsageThresholds? thresholds, out string? error)
    {
        thresholds = null;

        if (!TryParsePercent(WarningText, out int warning) || !TryParsePercent(CriticalText, out int critical))
        {
            error = Localizer.Format(Strings.Format_ThresholdRangeError, UsageThresholds.Minimum, UsageThresholds.Maximum);
            return false;
        }

        if (warning >= critical)
        {
            error = Strings.Threshold_OrderError;
            return false;
        }

        thresholds = new UsageThresholds(warning, critical);
        error = null;
        return true;
    }

    private static bool TryParsePercent(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, Localizer.Instance.Culture, out value) &&
        value is >= UsageThresholds.Minimum and <= UsageThresholds.Maximum;
}
