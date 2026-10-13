using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CobiHw.App.Settings;

public sealed partial class ThresholdRowViewModel : ObservableObject
{
    public ThresholdRowViewModel(string name, UsageThresholds thresholds)
    {
        Name = name;
        Reset(thresholds);
    }

    public string Name { get; }

    [ObservableProperty] private string _warningText = string.Empty;
    [ObservableProperty] private string _criticalText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => Error is not null;

    public void Reset(UsageThresholds thresholds)
    {
        WarningText = thresholds.Warning.ToString(CultureInfo.CurrentCulture);
        CriticalText = thresholds.Critical.ToString(CultureInfo.CurrentCulture);
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
            error = $"Use números inteiros de {UsageThresholds.Minimum} a {UsageThresholds.Maximum}.";
            return false;
        }

        if (warning >= critical)
        {
            error = "O amarelo precisa começar antes do vermelho.";
            return false;
        }

        thresholds = new UsageThresholds(warning, critical);
        error = null;
        return true;
    }

    private static bool TryParsePercent(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) &&
        value is >= UsageThresholds.Minimum and <= UsageThresholds.Maximum;
}
