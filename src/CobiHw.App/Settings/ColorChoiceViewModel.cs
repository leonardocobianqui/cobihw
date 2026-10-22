using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;

namespace CobiHw.App.Settings;

/// <summary>A color picker row in the appearance editor.</summary>
public sealed partial class ColorChoiceViewModel : ObservableObject
{
    private readonly string _default;
    private readonly string _labelKey;

    /// <summary>Creates the row.</summary>
    /// <param name="labelKey">Resource key of the label.</param>
    /// <param name="defaultColor">Color restored by "Restore defaults".</param>
    /// <param name="current">Saved color; invalid values fall back to the default.</param>
    public ColorChoiceViewModel(string labelKey, string defaultColor, string? current)
    {
        _labelKey = labelKey;
        _default = defaultColor;
        Load(current);
    }

    /// <summary>Translated label.</summary>
    public string Label => Localizer.Instance[_labelKey];

    // "#RRGGBB".
    [ObservableProperty] private string _color = string.Empty;

    private void Load(string? color) => Color = HexColor.Normalize(color, _default);

    /// <summary>Back to the default color.</summary>
    public void Reset() => Color = _default;

    /// <summary>Updates the label after a language change.</summary>
    public void RefreshLabel() => OnPropertyChanged(nameof(Label));
}

/// <summary>One option of the text weight box.</summary>
/// <param name="Weight">Weight value.</param>
/// <param name="Label">Translated name.</param>
public sealed record WeightOption(TextWeight Weight, string Label)
{
    /// <summary>Name WPF understands as a FontWeight, used to preview each option in its own weight.</summary>
    public string WeightName => Weight.ToString();
}

/// <summary>One option of the interaction key box.</summary>
/// <param name="Key">Key value.</param>
/// <param name="Label">Translated name.</param>
public sealed record KeyOption(InteractionKey Key, string Label);
