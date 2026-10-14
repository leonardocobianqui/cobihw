using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core;

namespace CobiHw.App.Settings;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ThresholdRowViewModel _cpu;
    private readonly ThresholdRowViewModel _gpu;
    private readonly ThresholdRowViewModel _memory;
    private readonly ThresholdRowViewModel _disk;
    private readonly Dictionary<MetricItem, MetricRowViewModel> _metricRows;
    private readonly ColorChoiceViewModel _normalColor;
    private readonly ColorChoiceViewModel _warningColor;
    private readonly ColorChoiceViewModel _criticalColor;
    private readonly IReadOnlyList<DiskActivity> _availableDisks;
    private SavedSettings _applied;

    public SettingsViewModel(SavedSettings current, IReadOnlyList<DiskActivity> availableDisks, SpeedTestViewModel speedTest)
    {
        _applied = current;
        SpeedTest = speedTest;

        ColorsEnabled = current.Colors.Enabled;
        _cpu = new ThresholdRowViewModel(MetricItem.Cpu, current.Colors.Cpu);
        _gpu = new ThresholdRowViewModel(MetricItem.Gpu, current.Colors.Gpu);
        _memory = new ThresholdRowViewModel(MetricItem.Memory, current.Colors.Memory);
        _disk = new ThresholdRowViewModel(MetricItem.Disk, current.Colors.Disk);

        Thresholds = [_cpu, _gpu, _memory, _disk];

        foreach (ThresholdRowViewModel row in Thresholds)
        {
            row.PropertyChanged += OnRowPropertyChanged;
        }

        _metricRows = Enum.GetValues<MetricItem>().ToDictionary(item => item, item => new MetricRowViewModel(item));
        Metrics = [.. Enum.GetValues<MetricItem>().Select(item => _metricRows[item])];

        foreach (MetricRowViewModel row in Metrics)
        {
            row.PropertyChanged += OnMetricRowPropertyChanged;
        }

        BackgroundColor = new ColorChoiceViewModel(nameof(Strings.Appearance_BackgroundColor), AppearanceSettings.DefaultBackgroundColor, current.Appearance.BackgroundColor);
        TextColor = new ColorChoiceViewModel(nameof(Strings.Appearance_TextColor), AppearanceSettings.DefaultTextColor, current.Appearance.TextColor);
        _normalColor = new ColorChoiceViewModel(nameof(Strings.Level_Normal), ColorSettings.DefaultNormalColor, current.Colors.NormalColor);
        _warningColor = new ColorChoiceViewModel(nameof(Strings.Level_Warning), ColorSettings.DefaultWarningColor, current.Colors.WarningColor);
        _criticalColor = new ColorChoiceViewModel(nameof(Strings.Level_Critical), ColorSettings.DefaultCriticalColor, current.Colors.CriticalColor);
        LevelColors = [_normalColor, _warningColor, _criticalColor];

        foreach (ColorChoiceViewModel choice in AllColors)
        {
            choice.PropertyChanged += OnRowPropertyChanged;
        }

        Transparency = Math.Clamp(current.Appearance.Transparency, 0, AppearanceSettings.MaximumTransparency);
        TextWeight = Enum.IsDefined(current.Appearance.TextWeight) ? current.Appearance.TextWeight : TextWeight.Normal;

        _availableDisks = availableDisks;
        _diskOptions = BuildDiskOptions(availableDisks, current.Display);
        _weightOptions = BuildWeightOptions();
        _languageOptions = Localizer.Languages();
        Language = _languageOptions.Any(option => option.Code == current.Language) ? current.Language : Localizer.AutomaticLanguage;
        LoadDisplay(current.Display);
        PingHostText = current.Network.PingHost;
        Localizer.Instance.CultureChanged += OnCultureChanged;
    }

    public event EventHandler<SavedSettings>? Applied;

    [ObservableProperty] private IReadOnlyList<DiskOption> _diskOptions;

    [ObservableProperty] private IReadOnlyList<WeightOption> _weightOptions;

    [ObservableProperty] private IReadOnlyList<LanguageOption> _languageOptions;

    [ObservableProperty] private IReadOnlyList<KeyOption> _keyOptions = BuildKeyOptions();

    public ObservableCollection<ThresholdRowViewModel> Thresholds { get; }

    public ObservableCollection<MetricRowViewModel> Metrics { get; }

    public ColorChoiceViewModel BackgroundColor { get; }

    public ColorChoiceViewModel TextColor { get; }

    public IReadOnlyList<ColorChoiceViewModel> LevelColors { get; }

    private IEnumerable<ColorChoiceViewModel> AllColors => [BackgroundColor, TextColor, .. LevelColors];


    public IReadOnlyList<string> PingSuggestions { get; } = [NetworkSettings.GooglePingHost, NetworkSettings.CloudflarePingHost];

    public SpeedTestViewModel SpeedTest { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private DiskOption? _selectedDisk;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _detailedTooltip;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _clickThrough;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private InteractionKey _interactionKey;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _colorsEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _transparency;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private TextWeight _textWeight;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private string _language = Localizer.AutomaticLanguage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPingHostError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private string _pingHostText = string.Empty;

    public bool HasDisplayError => !Metrics.Any(row => row.IsVisible);

    public bool HasPingHostError => Uri.CheckHostName(PingHostText.Trim()) == UriHostNameType.Unknown;

    private bool CanApply() =>
        !HasDisplayError &&
        !HasPingHostError &&
        Thresholds.All(row => !row.HasError) &&
        BuildSettings() != _applied;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        _applied = BuildSettings();
        Applied?.Invoke(this, _applied);
        ApplyCommand.NotifyCanExecuteChanged();
    }

    public void MoveMetric(MetricRowViewModel row, int newIndex)
    {
        int oldIndex = Metrics.IndexOf(row);
        if (oldIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        Metrics.Move(oldIndex, newIndex);
        ApplyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        LoadDisplay(new DisplaySettings());
        ColorsEnabled = true;
        PingHostText = new NetworkSettings().PingHost;
        Transparency = 0;
        TextWeight = TextWeight.Normal;
        Language = Localizer.AutomaticLanguage;

        foreach (ColorChoiceViewModel choice in AllColors)
        {
            choice.Reset();
        }

        foreach (ThresholdRowViewModel row in Thresholds)
        {
            row.Reset(UsageThresholds.Default);
        }
    }

    private SavedSettings BuildSettings() => new(
        new DisplaySettings
        {
            ShowCpu = _metricRows[MetricItem.Cpu].IsVisible,
            ShowGpu = _metricRows[MetricItem.Gpu].IsVisible,
            ShowMemory = _metricRows[MetricItem.Memory].IsVisible,
            ShowDisk = _metricRows[MetricItem.Disk].IsVisible,
            ShowNetwork = _metricRows[MetricItem.Network].IsVisible,
            Order = new MetricOrder([.. Metrics.Select(row => row.Item)]),
            DiskSource = SelectedDisk?.Source ?? DiskSource.System,
            DiskNumber = SelectedDisk?.Number,
            DetailedTooltip = DetailedTooltip,
            ClickThrough = ClickThrough,
            InteractionKey = InteractionKey,
        },
        new ColorSettings
        {
            Enabled = ColorsEnabled,
            NormalColor = _normalColor.Color,
            WarningColor = _warningColor.Color,
            CriticalColor = _criticalColor.Color,
            Cpu = _cpu.ToThresholds(),
            Gpu = _gpu.ToThresholds(),
            Memory = _memory.ToThresholds(),
            Disk = _disk.ToThresholds(),
        },
        new NetworkSettings { PingHost = PingHostText.Trim() },
        new AppearanceSettings
        {
            BackgroundColor = BackgroundColor.Color,
            Transparency = Transparency,
            TextColor = TextColor.Color,
            TextWeight = TextWeight,
        },
        Language);

    private static List<DiskOption> BuildDiskOptions(IReadOnlyList<DiskActivity> availableDisks, DisplaySettings saved)
    {
        DiskActivity? systemDisk = availableDisks.FirstOrDefault(disk => disk.IsSystem);

        List<DiskOption> options =
        [
            new DiskOption(
                DiskSource.System,
                null,
                systemDisk is null ? Strings.Disk_Windows : Localizer.Format(Strings.Format_DiskWindows, DisplayFormat.DiskLabel(systemDisk))),
        ];

        options.AddRange(availableDisks
            .Where(disk => !disk.IsSystem)
            .Select(disk => new DiskOption(DiskSource.Specific, disk.Number, Localizer.Format(Strings.Format_DiskNumber, DisplayFormat.DiskLabel(disk)))));

        if (saved.DiskSource == DiskSource.Specific &&
            saved.DiskNumber is int number &&
            availableDisks.All(disk => disk.Number != number))
        {
            options.Add(new DiskOption(DiskSource.Specific, number, Localizer.Format(Strings.Format_DiskOptionNotFound, number)));
        }

        return options;
    }

    private static List<WeightOption> BuildWeightOptions() =>
    [
        new(TextWeight.Light, Strings.Weight_Light),
        new(TextWeight.Normal, Strings.Weight_Normal),
        new(TextWeight.Medium, Strings.Weight_Medium),
        new(TextWeight.SemiBold, Strings.Weight_SemiBold),
        new(TextWeight.Bold, Strings.Weight_Bold),
    ];

    private static List<KeyOption> BuildKeyOptions() =>
        [.. Enum.GetValues<InteractionKey>().Select(key => new KeyOption(key, ClickThroughRule.LabelOf(key)))];

    public void SyncClickThrough(bool enabled)
    {
        ClickThrough = enabled;
        _applied = _applied with { Display = _applied.Display with { ClickThrough = enabled } };
        ApplyCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() => Localizer.Instance.CultureChanged -= OnCultureChanged;

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (ThresholdRowViewModel row in Thresholds)
        {
            row.RefreshTexts();
        }

        foreach (MetricRowViewModel row in Metrics)
        {
            row.RefreshName();
        }

        foreach (ColorChoiceViewModel choice in AllColors)
        {
            choice.RefreshLabel();
        }

        DiskOption? disk = SelectedDisk;
        TextWeight weight = TextWeight;
        string language = Language;
        InteractionKey interactionKey = InteractionKey;

        DiskOptions = BuildDiskOptions(_availableDisks, _applied.Display);
        WeightOptions = BuildWeightOptions();
        LanguageOptions = Localizer.Languages();
        KeyOptions = BuildKeyOptions();

        SelectedDisk = (disk is null ? null : DiskOptions.FirstOrDefault(option => option.Source == disk.Source && option.Number == disk.Number)) ?? DiskOptions[0];
        TextWeight = weight;
        Language = language;
        InteractionKey = interactionKey;
        OnPropertyChanged(nameof(TextWeight));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(InteractionKey));
    }

    private void LoadDisplay(DisplaySettings display)
    {
        for (int index = 0; index < display.Order.Items.Count; index++)
        {
            MetricRowViewModel row = _metricRows[display.Order.Items[index]];
            MoveMetric(row, index);
            row.IsVisible = display.IsShown(row.Item);
        }

        SelectedDisk = DiskOptions.FirstOrDefault(option => option.Matches(display)) ?? DiskOptions[0];
        DetailedTooltip = display.DetailedTooltip;
        ClickThrough = display.ClickThrough;
        InteractionKey = Enum.IsDefined(display.InteractionKey) ? display.InteractionKey : InteractionKey.Ctrl;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        ApplyCommand.NotifyCanExecuteChanged();

    private void OnMetricRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MetricRowViewModel.IsVisible))
        {
            return;
        }

        OnPropertyChanged(nameof(HasDisplayError));
        ApplyCommand.NotifyCanExecuteChanged();
    }
}

public sealed record DiskOption(DiskSource Source, int? Number, string Label)
{
    public bool Matches(DisplaySettings display) =>
        Source == display.DiskSource && (Source != DiskSource.Specific || Number == display.DiskNumber);
}
