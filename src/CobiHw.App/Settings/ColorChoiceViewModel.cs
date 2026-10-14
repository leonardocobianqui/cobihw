using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;

namespace CobiHw.App.Settings;

public sealed partial class ColorChoiceViewModel : ObservableObject
{
    private readonly string _default;
    private readonly string _labelKey;

    public ColorChoiceViewModel(string labelKey, string defaultColor, string? current)
    {
        _labelKey = labelKey;
        _default = defaultColor;
        Load(current);
    }

    public string Label => Localizer.Instance[_labelKey];

    [ObservableProperty] private string _color = string.Empty;

    private void Load(string? color) => Color = HexColor.Normalize(color, _default);

    public void Reset() => Color = _default;

    public void RefreshLabel() => OnPropertyChanged(nameof(Label));
}

public sealed record WeightOption(TextWeight Weight, string Label)
{
    public string WeightName => Weight.ToString();
}

public sealed record KeyOption(InteractionKey Key, string Label);
