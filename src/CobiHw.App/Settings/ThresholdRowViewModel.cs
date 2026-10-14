using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;
using CobiHw.App.Resources;

namespace CobiHw.App.Settings;

public sealed partial class ThresholdRowViewModel : ObservableObject
{
    private readonly MetricItem _item;

    public ThresholdRowViewModel(MetricItem item, UsageThresholds thresholds)
    {
        _item = item;
        Reset(thresholds);
    }

    public string Name => MetricRowViewModel.NameOf(_item);

    [ObservableProperty] private string _warningText = string.Empty;
    [ObservableProperty] private string _criticalText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => Error is not null;

    public void Reset(UsageThresholds thresholds)
    {
        WarningText = thresholds.Warning.ToString(Localizer.Instance.Culture);
        CriticalText = thresholds.Critical.ToString(Localizer.Instance.Culture);
    }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Name));
        Validate();
    }

    public UsageThresholds ToThresholds() =>
        TryParse(out UsageThresholds? thresholds, out _)
            ? thresholds
            : throw new InvalidOperationException($"Limites inválidos para {Name}: {Error}");

    partial void OnWarningTextChanged(string value) => Validate();

    partial void OnCriticalTextChanged(string value) => Validate();

    private void Validate()
    {
        TryParse(out _, out string? error);
        Error = error;
    }

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
