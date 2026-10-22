using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;

namespace CobiHw.App.Settings;

/// <summary>Groups of the Appearance tab; each opens its own editor window.</summary>
public enum AppearanceGroup
{
    /// <summary>Size, corners, bars and border.</summary>
    Shape,
    /// <summary>Background, transparency, text color and weight.</summary>
    TextColors,
    /// <summary>Usage colors and thresholds.</summary>
    UsageColors,
}

/// <summary>Appearance and usage colors shown together on the widget.</summary>
/// <param name="Appearance">Look of the widget.</param>
/// <param name="Colors">Usage colors.</param>
public sealed record AppearancePreview(AppearanceSettings Appearance, ColorSettings Colors);

/// <summary>One option of the border box.</summary>
/// <param name="Mode">Border mode.</param>
/// <param name="Label">Translated name.</param>
public sealed record BorderModeOption(BorderMode Mode, string Label);

/// <summary>
/// State of one appearance editor window. Every change is previewed on the widget right away; nothing is saved until "Apply".
/// </summary>
public sealed partial class AppearanceEditorViewModel : ObservableObject
{
    private readonly AppearancePreview _initial;
    private readonly ThresholdRowViewModel _cpu;
    private readonly ThresholdRowViewModel _gpu;
    private readonly ThresholdRowViewModel _memory;
    private readonly ThresholdRowViewModel _disk;
    private readonly ColorChoiceViewModel _normalColor;
    private readonly ColorChoiceViewModel _warningColor;
    private readonly ColorChoiceViewModel _criticalColor;
    // Last valid usage colors: while a threshold is invalid, the preview keeps these.
    private ColorSettings _previewColors;
    // Avoids sending the same preview twice.
    private AppearancePreview _lastPreview;
    private readonly bool _ready;

    /// <summary>Loads the values currently shown on the widget; out-of-range values are clamped.</summary>
    /// <param name="group">Which group this window edits.</param>
    /// <param name="appearance">Current appearance.</param>
    /// <param name="colors">Current usage colors.</param>
    public AppearanceEditorViewModel(AppearanceGroup group, AppearanceSettings appearance, ColorSettings colors)
    {
        Group = group;

        BackgroundColor = new ColorChoiceViewModel(nameof(Strings.Appearance_BackgroundColor), AppearanceSettings.DefaultBackgroundColor, appearance.BackgroundColor);
        TextColor = new ColorChoiceViewModel(nameof(Strings.Appearance_TextColor), AppearanceSettings.DefaultTextColor, appearance.TextColor);
        BorderColor = new ColorChoiceViewModel(nameof(Strings.Appearance_BorderColor), AppearanceSettings.DefaultTextColor, appearance.BorderColor);
        _normalColor = new ColorChoiceViewModel(nameof(Strings.Level_Normal), ColorSettings.DefaultNormalColor, colors.NormalColor);
        _warningColor = new ColorChoiceViewModel(nameof(Strings.Level_Warning), ColorSettings.DefaultWarningColor, colors.WarningColor);
        _criticalColor = new ColorChoiceViewModel(nameof(Strings.Level_Critical), ColorSettings.DefaultCriticalColor, colors.CriticalColor);
        LevelColors = [_normalColor, _warningColor, _criticalColor];

        _cpu = new ThresholdRowViewModel(MetricItem.Cpu, colors.Cpu);
        _gpu = new ThresholdRowViewModel(MetricItem.Gpu, colors.Gpu);
        _memory = new ThresholdRowViewModel(MetricItem.Memory, colors.Memory);
        _disk = new ThresholdRowViewModel(MetricItem.Disk, colors.Disk);
        Thresholds = [_cpu, _gpu, _memory, _disk];

        ObservableObject[] children = [BackgroundColor, TextColor, BorderColor, .. LevelColors, .. Thresholds];

        foreach (ObservableObject child in children)
        {
            child.PropertyChanged += OnChildPropertyChanged;
        }

        Transparency = Math.Clamp(appearance.Transparency, 0, AppearanceSettings.MaximumTransparency);
        TextWeight = Enum.IsDefined(appearance.TextWeight) ? appearance.TextWeight : TextWeight.Normal;
        Scale = ResizeRule.ClampPercent(appearance.Scale);
        BorderMode = Enum.IsDefined(appearance.BorderMode) ? appearance.BorderMode : BorderMode.Off;
        BorderThickness = Math.Clamp(appearance.BorderThickness, AppearanceSettings.MinimumBorderThickness, AppearanceSettings.MaximumBorderThickness);
        BorderTransparency = Math.Clamp(appearance.BorderTransparency, 0, AppearanceSettings.MaximumTransparency);
        CornerRadius = CornerRule.Clamp(appearance.CornerRadius);
        BarRadius = Math.Clamp(appearance.BarRadius, 0, AppearanceSettings.MaximumBarRadius);
        ColorsEnabled = colors.Enabled;

        _previewColors = HasColorsError ? colors : BuildColors();
        _initial = new AppearancePreview(BuildAppearance(), _previewColors);
        _lastPreview = _initial;
        _ready = true;
    }

    /// <summary>Raised on every change, so the widget can show it.</summary>
    public event EventHandler<AppearancePreview>? Previewed;

    /// <summary>Raised by "Apply" with the final values.</summary>
    public event EventHandler<AppearancePreview>? Applied;

    /// <summary>Group this window edits.</summary>
    public AppearanceGroup Group { get; }

    /// <summary>Shows the shape section.</summary>
    public bool IsShape => Group == AppearanceGroup.Shape;

    /// <summary>Shows the colors and text section.</summary>
    public bool IsTextColors => Group == AppearanceGroup.TextColors;

    /// <summary>Shows the usage colors section.</summary>
    public bool IsUsageColors => Group == AppearanceGroup.UsageColors;

    /// <summary>Window title for the group.</summary>
    public string Title => Group switch
    {
        AppearanceGroup.Shape => Strings.Section_AppearanceShape,
        AppearanceGroup.TextColors => Strings.Section_AppearanceColors,
        _ => Strings.Section_AppearanceUsage,
    };

    public ColorChoiceViewModel BackgroundColor { get; }

    public ColorChoiceViewModel TextColor { get; }

    public ColorChoiceViewModel BorderColor { get; }

    /// <summary>Normal, warning and critical colors.</summary>
    public IReadOnlyList<ColorChoiceViewModel> LevelColors { get; }

    /// <summary>Warning/critical thresholds of CPU, GPU, RAM and disk.</summary>
    public ObservableCollection<ThresholdRowViewModel> Thresholds { get; }

    /// <summary>Options of the text weight box.</summary>
    public IReadOnlyList<WeightOption> WeightOptions { get; } =
    [
        new(TextWeight.Light, Strings.Weight_Light),
        new(TextWeight.Normal, Strings.Weight_Normal),
        new(TextWeight.Medium, Strings.Weight_Medium),
        new(TextWeight.SemiBold, Strings.Weight_SemiBold),
        new(TextWeight.Bold, Strings.Weight_Bold),
    ];

    /// <summary>Options of the border box.</summary>
    public IReadOnlyList<BorderModeOption> BorderModeOptions { get; } =
    [
        new(BorderMode.Off, Strings.BorderMode_Off),
        new(BorderMode.Always, Strings.BorderMode_Always),
        new(BorderMode.Hover, Strings.BorderMode_Hover),
    ];

    [ObservableProperty] private int _transparency;

    [ObservableProperty] private TextWeight _textWeight;

    [ObservableProperty] private int _scale = AppearanceSettings.DefaultScale;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBorder))]
    private BorderMode _borderMode;

    [ObservableProperty] private int _borderThickness = AppearanceSettings.MinimumBorderThickness;

    [ObservableProperty] private int _borderTransparency = AppearanceSettings.DefaultBorderTransparency;

    [ObservableProperty] private int _cornerRadius;

    [ObservableProperty] private int _barRadius;

    [ObservableProperty] private bool _colorsEnabled;

    /// <summary>Shows the border color, thickness and transparency controls.</summary>
    public bool HasBorder => BorderMode != BorderMode.Off;

    public int MaximumCornerRadius => CornerRule.MaximumRadius;

    public int MaximumBarRadius => AppearanceSettings.MaximumBarRadius;

    /// <summary>Hint with the maximum corner radius.</summary>
    public string CornerRadiusHint => Localizer.Format(Strings.Format_CornerRadiusHint, CornerRule.MaximumRadius);

    /// <summary>True while any threshold is invalid.</summary>
    public bool HasColorsError => Thresholds.Any(row => row.HasError);

    /// <summary>True when the values differ from those the window opened with.</summary>
    public bool IsPending => HasColorsError || Build() != _initial;

    /// <summary>Enables "Apply".</summary>
    public bool CanApply => IsPending && !HasColorsError;

    // Hands the result to the settings window, which applies it.
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply() => Applied?.Invoke(this, Build());

    [RelayCommand]
    private void ResetScale() => Scale = AppearanceSettings.DefaultScale;

    // Restores only this group's values.
    [RelayCommand]
    private void RestoreDefaults()
    {
        switch (Group)
        {
            case AppearanceGroup.Shape:
                Scale = AppearanceSettings.DefaultScale;
                CornerRadius = 0;
                BarRadius = 0;
                BorderMode = BorderMode.Off;
                BorderColor.Reset();
                BorderThickness = AppearanceSettings.MinimumBorderThickness;
                BorderTransparency = AppearanceSettings.DefaultBorderTransparency;
                break;
            case AppearanceGroup.TextColors:
                BackgroundColor.Reset();
                Transparency = 0;
                TextColor.Reset();
                TextWeight = TextWeight.Normal;
                break;
            default:
                ColorsEnabled = true;

                foreach (ColorChoiceViewModel choice in LevelColors)
                {
                    choice.Reset();
                }

                foreach (ThresholdRowViewModel row in Thresholds)
                {
                    row.Reset(UsageThresholds.Default);
                }

                break;
        }
    }

    // Every change (here or in a child row) refreshes the preview.
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Refresh();
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (!_ready)
        {
            return;
        }

        ApplyCommand.NotifyCanExecuteChanged();

        if (!HasColorsError)
        {
            _previewColors = BuildColors();
        }

        AppearancePreview preview = new(BuildAppearance(), _previewColors);

        if (preview == _lastPreview)
        {
            return;
        }

        _lastPreview = preview;
        Previewed?.Invoke(this, preview);
    }

    // With invalid thresholds, the last valid colors are used.
    private AppearancePreview Build() => new(BuildAppearance(), HasColorsError ? _previewColors : BuildColors());

    private AppearanceSettings BuildAppearance() => new()
    {
        BackgroundColor = BackgroundColor.Color,
        Transparency = Transparency,
        TextColor = TextColor.Color,
        TextWeight = TextWeight,
        Scale = Scale,
        BorderMode = BorderMode,
        BorderColor = BorderColor.Color,
        BorderThickness = BorderThickness,
        BorderTransparency = BorderTransparency,
        CornerRadius = CornerRadius,
        BarRadius = BarRadius,
    };

    private ColorSettings BuildColors() => new()
    {
        Enabled = ColorsEnabled,
        NormalColor = _normalColor.Color,
        WarningColor = _warningColor.Color,
        CriticalColor = _criticalColor.Color,
        Cpu = _cpu.ToThresholds(),
        Gpu = _gpu.ToThresholds(),
        Memory = _memory.ToThresholds(),
        Disk = _disk.ToThresholds(),
    };
}
