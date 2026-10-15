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
    private readonly IReadOnlyList<DisplayMonitor> _monitors;
    private SavedSettings _applied;
    private string? _fpsNoticeKey;
    private bool _ready;

    public SettingsViewModel(SavedSettings current, IReadOnlyList<DiskActivity> availableDisks, IReadOnlyList<DisplayMonitor> monitors, SpeedTestViewModel speedTest)
    {
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

        _metricRows = MetricOrder.Orderable.ToDictionary(item => item, item => new MetricRowViewModel(item));
        Metrics = [.. MetricOrder.Orderable.Select(item => _metricRows[item])];

        foreach (MetricRowViewModel row in Metrics)
        {
            row.PropertyChanged += OnMetricRowPropertyChanged;
        }

        BackgroundColor = new ColorChoiceViewModel(nameof(Strings.Appearance_BackgroundColor), AppearanceSettings.DefaultBackgroundColor, current.Appearance.BackgroundColor);
        TextColor = new ColorChoiceViewModel(nameof(Strings.Appearance_TextColor), AppearanceSettings.DefaultTextColor, current.Appearance.TextColor);
        BorderColor = new ColorChoiceViewModel(nameof(Strings.Appearance_BorderColor), AppearanceSettings.DefaultTextColor, current.Appearance.BorderColor);
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
        Scale = ResizeRule.ClampPercent(current.Appearance.Scale);
        BorderMode = Enum.IsDefined(current.Appearance.BorderMode) ? current.Appearance.BorderMode : BorderMode.Off;
        BorderThickness = Math.Clamp(current.Appearance.BorderThickness, AppearanceSettings.MinimumBorderThickness, AppearanceSettings.MaximumBorderThickness);
        BorderTransparency = Math.Clamp(current.Appearance.BorderTransparency, 0, AppearanceSettings.MaximumTransparency);
        CornerRadius = CornerRule.Clamp(current.Appearance.CornerRadius);

        _availableDisks = availableDisks;
        _diskOptions = BuildDiskOptions(availableDisks, current.Display);
        _weightOptions = BuildWeightOptions();
        _languageOptions = Localizer.Languages();
        _monitors = monitors;
        _monitorOptions = BuildMonitorOptions(monitors);
        _modeOptions = BuildModeOptions();
        LoadGeneral(current.General);
        LoadDisplay(current.Display);
        PingHostText = current.Network.PingHost;
        Localizer.Instance.CultureChanged += OnCultureChanged;

        _applied = new SavedSettings(BuildDisplay(), HasColorsError ? current.Colors : BuildColors(), BuildNetwork(), BuildAppearance(), BuildGeneral());
        _ready = true;
    }

    public event EventHandler<SavedSettings>? Applied;

    [ObservableProperty] private IReadOnlyList<DiskOption> _diskOptions;

    [ObservableProperty] private IReadOnlyList<WeightOption> _weightOptions;

    [ObservableProperty] private IReadOnlyList<LanguageOption> _languageOptions;

    [ObservableProperty] private IReadOnlyList<KeyOption> _keyOptions = BuildKeyOptions();

    [ObservableProperty] private IReadOnlyList<MonitorOption> _monitorOptions;

    [ObservableProperty] private IReadOnlyList<ModeOption> _modeOptions;

    [ObservableProperty] private IReadOnlyList<ThemeOption> _themeOptions = BuildThemeOptions();

    [ObservableProperty] private IReadOnlyList<BorderModeOption> _borderModeOptions = BuildBorderModeOptions();

    [ObservableProperty]
    private SettingsTab _selectedTab;

    public ObservableCollection<ThresholdRowViewModel> Thresholds { get; }

    public ObservableCollection<MetricRowViewModel> Metrics { get; }

    public ColorChoiceViewModel BackgroundColor { get; }

    public ColorChoiceViewModel TextColor { get; }

    public ColorChoiceViewModel BorderColor { get; }

    public IReadOnlyList<ColorChoiceViewModel> LevelColors { get; }

    private IEnumerable<ColorChoiceViewModel> AllColors => [BackgroundColor, TextColor, BorderColor, .. LevelColors];


    public IReadOnlyList<string> PingSuggestions { get; } = [NetworkSettings.GooglePingHost, NetworkSettings.CloudflarePingHost];

    public SpeedTestViewModel SpeedTest { get; }

    [ObservableProperty]
    private DiskOption? _selectedDisk;

    [ObservableProperty]
    private bool _showFps;

    [ObservableProperty]
    private bool _detailedTooltip;

    [ObservableProperty]
    private bool _clickThrough;

    [ObservableProperty]
    private InteractionKey _interactionKey;

    [ObservableProperty]
    private bool _colorsEnabled;

    [ObservableProperty]
    private int _transparency;

    [ObservableProperty]
    private TextWeight _textWeight;

    [ObservableProperty]
    private int _scale = AppearanceSettings.DefaultScale;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBorder))]
    private BorderMode _borderMode;

    [ObservableProperty]
    private int _borderThickness = AppearanceSettings.MinimumBorderThickness;

    [ObservableProperty]
    private int _borderTransparency = AppearanceSettings.DefaultBorderTransparency;

    [ObservableProperty]
    private int _cornerRadius;

    public int MaximumCornerRadius => CornerRule.MaximumRadius;

    public string CornerRadiusHint => Localizer.Format(Strings.Format_CornerRadiusHint, CornerRule.MaximumRadius);

    [ObservableProperty]
    private string _language = Localizer.AutomaticLanguage;

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private string? _monitor;

    [ObservableProperty]
    private WidgetMode _mode;

    [ObservableProperty]
    private bool _snapToEdges;

    [ObservableProperty]
    private bool _positionLocked;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPingHostError))]
    private string _pingHostText = string.Empty;

    public bool HasDisplayError => !Metrics.Any(row => row.IsVisible);

    public bool HasPingHostError => Uri.CheckHostName(PingHostText.Trim()) == UriHostNameType.Unknown;

    public bool HasSeveralMonitors => _monitors.Count > 1;

    public bool HasBorder => BorderMode != BorderMode.Off;

    public string? FpsNotice => _fpsNoticeKey is null ? null : Localizer.Instance[_fpsNoticeKey];

    public bool HasFpsNotice => _fpsNoticeKey is not null;

    public bool IsGeneralPending => BuildGeneral() != _applied.General;

    public bool IsDisplayPending => BuildDisplay() != _applied.Display;

    public bool IsAppearancePending => BuildAppearance() != _applied.Appearance || HasColorsError || BuildColors() != _applied.Colors;

    public bool IsNetworkPending => BuildNetwork() != _applied.Network;

    public bool HasPendingChanges => IsGeneralPending || IsDisplayPending || IsAppearancePending || IsNetworkPending;

    public bool CanApplyAll => !HasDisplayError && !HasPingHostError && !HasColorsError && HasPendingChanges;

    public string PendingTabNames => string.Join(", ", new (bool Pending, string Name)[]
    {
        (IsGeneralPending, Strings.Tab_General),
        (IsDisplayPending, Strings.Tab_Display),
        (IsAppearancePending, Strings.Tab_Appearance),
        (IsNetworkPending, Strings.Tab_Network),
    }.Where(tab => tab.Pending).Select(tab => tab.Name));

    private bool HasColorsError => Thresholds.Any(row => row.HasError);

    private bool CanApply() => SelectedTab switch
    {
        SettingsTab.Display => !HasDisplayError && IsDisplayPending,
        SettingsTab.Appearance => !HasColorsError && IsAppearancePending,
        SettingsTab.Network => !HasPingHostError && IsNetworkPending,
        _ => IsGeneralPending,
    };

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply() => Commit(SelectedTab switch
    {
        SettingsTab.Display => _applied with { Display = BuildDisplay() },
        SettingsTab.Appearance => _applied with { Appearance = BuildAppearance(), Colors = BuildColors() },
        SettingsTab.Network => _applied with { Network = BuildNetwork() },
        _ => _applied with { General = BuildGeneral() },
    });

    public void ApplyAll()
    {
        if (CanApplyAll)
        {
            Commit(BuildSettings());
        }
    }

    public void MoveMetric(MetricRowViewModel row, int newIndex)
    {
        int oldIndex = Metrics.IndexOf(row);
        if (oldIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        Metrics.Move(oldIndex, newIndex);
        RefreshPending();
    }

    [RelayCommand]
    private void ResetScale() => Scale = AppearanceSettings.DefaultScale;

    [RelayCommand]
    private void RestoreDefaults()
    {
        switch (SelectedTab)
        {
            case SettingsTab.Display:
                LoadDisplay(new DisplaySettings());
                break;
            case SettingsTab.Appearance:
                RestoreAppearance();
                RestoreColors();
                break;
            case SettingsTab.Network:
                PingHostText = new NetworkSettings().PingHost;
                break;
            default:
                LoadGeneral(new GeneralSettings());
                break;
        }
    }

    private void RestoreAppearance()
    {
        BackgroundColor.Reset();
        Transparency = 0;
        TextColor.Reset();
        TextWeight = TextWeight.Normal;
        Scale = AppearanceSettings.DefaultScale;
        BorderMode = BorderMode.Off;
        BorderColor.Reset();
        BorderThickness = AppearanceSettings.MinimumBorderThickness;
        BorderTransparency = AppearanceSettings.DefaultBorderTransparency;
        CornerRadius = 0;
    }

    private void RestoreColors()
    {
        ColorsEnabled = true;

        foreach (ColorChoiceViewModel choice in LevelColors)
        {
            choice.Reset();
        }

        foreach (ThresholdRowViewModel row in Thresholds)
        {
            row.Reset(UsageThresholds.Default);
        }
    }

    private void Commit(SavedSettings settings)
    {
        _applied = settings;
        Applied?.Invoke(this, settings);
        RefreshPending();
    }

    private SavedSettings BuildSettings() => new(BuildDisplay(), BuildColors(), BuildNetwork(), BuildAppearance(), BuildGeneral());

    private GeneralSettings BuildGeneral() => new()
    {
        Language = Language,
        Theme = Theme,
        Mode = Mode,
        SnapToEdges = SnapToEdges,
        PositionLocked = PositionLocked,
        ClickThrough = ClickThrough,
        InteractionKey = InteractionKey,
        StartWithWindows = StartWithWindows,
        Monitor = Monitor,
    };

    private DisplaySettings BuildDisplay() => new()
    {
        ShowCpu = _metricRows[MetricItem.Cpu].IsVisible,
        ShowGpu = _metricRows[MetricItem.Gpu].IsVisible,
        ShowMemory = _metricRows[MetricItem.Memory].IsVisible,
        ShowDisk = _metricRows[MetricItem.Disk].IsVisible,
        ShowNetwork = _metricRows[MetricItem.Network].IsVisible,
        ShowFps = ShowFps,
        Order = new MetricOrder([.. Metrics.Select(row => row.Item)]),
        DiskSource = SelectedDisk?.Source ?? DiskSource.System,
        DiskNumber = SelectedDisk?.Number,
        DetailedTooltip = DetailedTooltip,
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

    private NetworkSettings BuildNetwork() => new() { PingHost = PingHostText.Trim() };

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
    };

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

    private static List<ModeOption> BuildModeOptions() =>
    [
        new(WidgetMode.AlwaysOnTop, Strings.Tray_AlwaysOnTop),
        new(WidgetMode.Desktop, Strings.Tray_Desktop),
    ];

    private static List<ThemeOption> BuildThemeOptions() =>
    [
        new(AppTheme.System, Strings.Theme_System),
        new(AppTheme.Light, Strings.Theme_Light),
        new(AppTheme.Dark, Strings.Theme_Dark),
    ];

    private static List<BorderModeOption> BuildBorderModeOptions() =>
    [
        new(BorderMode.Off, Strings.BorderMode_Off),
        new(BorderMode.Always, Strings.BorderMode_Always),
        new(BorderMode.Hover, Strings.BorderMode_Hover),
    ];

    private static List<MonitorOption> BuildMonitorOptions(IReadOnlyList<DisplayMonitor> monitors) =>
    [
        .. monitors.Select(monitor => new MonitorOption(
            monitor.Device,
            monitor.IsPrimary,
            Localizer.Format(monitor.IsPrimary ? Strings.Format_MonitorPrimary : Strings.Format_Monitor, monitor.Number, monitor.Width, monitor.Height))),
    ];

    public void SyncGeneral(GeneralSettings general)
    {
        GeneralSettings applied = _applied.General;

        if (general.Mode != applied.Mode)
        {
            Mode = general.Mode;
        }

        if (general.SnapToEdges != applied.SnapToEdges)
        {
            SnapToEdges = general.SnapToEdges;
        }

        if (general.PositionLocked != applied.PositionLocked)
        {
            PositionLocked = general.PositionLocked;
        }

        if (general.ClickThrough != applied.ClickThrough)
        {
            ClickThrough = general.ClickThrough;
        }

        if (general.StartWithWindows != applied.StartWithWindows)
        {
            StartWithWindows = general.StartWithWindows;
        }

        if (general.Monitor != applied.Monitor)
        {
            Monitor = general.Monitor;
        }

        _applied = _applied with { General = general with { Language = applied.Language, Theme = applied.Theme } };
        RefreshPending();
    }

    public void RejectFps(string noticeKey)
    {
        ShowFps = false;
        _applied = _applied with { Display = _applied.Display with { ShowFps = false } };
        ShowFpsNotice(noticeKey);
    }

    public void ShowFpsNotice(string noticeKey)
    {
        _fpsNoticeKey = noticeKey;
        OnPropertyChanged(nameof(FpsNotice));
        OnPropertyChanged(nameof(HasFpsNotice));
    }

    public void SyncScale(int percent)
    {
        Scale = percent;
        _applied = _applied with { Appearance = _applied.Appearance with { Scale = percent } };
        RefreshPending();
    }

    public void Dispose() => Localizer.Instance.CultureChanged -= OnCultureChanged;

    partial void OnShowFpsChanged(bool value)
    {
        _fpsNoticeKey = null;
        OnPropertyChanged(nameof(FpsNotice));
        OnPropertyChanged(nameof(HasFpsNotice));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is not (nameof(IsGeneralPending) or nameof(IsDisplayPending) or nameof(IsAppearancePending) or nameof(IsNetworkPending)))
        {
            RefreshPending();
        }
    }

    private void RefreshPending()
    {
        if (!_ready)
        {
            return;
        }

        OnPropertyChanged(nameof(IsGeneralPending));
        OnPropertyChanged(nameof(IsDisplayPending));
        OnPropertyChanged(nameof(IsAppearancePending));
        OnPropertyChanged(nameof(IsNetworkPending));
        ApplyCommand.NotifyCanExecuteChanged();
    }

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

        OnPropertyChanged(nameof(FpsNotice));
        OnPropertyChanged(nameof(CornerRadiusHint));

        DiskOption? disk = SelectedDisk;
        TextWeight weight = TextWeight;
        string language = Language;
        InteractionKey interactionKey = InteractionKey;
        string? monitor = Monitor;
        WidgetMode mode = Mode;
        BorderMode borderMode = BorderMode;
        AppTheme theme = Theme;

        DiskOptions = BuildDiskOptions(_availableDisks, _applied.Display);
        WeightOptions = BuildWeightOptions();
        LanguageOptions = Localizer.Languages();
        KeyOptions = BuildKeyOptions();
        MonitorOptions = BuildMonitorOptions(_monitors);
        ModeOptions = BuildModeOptions();
        BorderModeOptions = BuildBorderModeOptions();
        ThemeOptions = BuildThemeOptions();

        SelectedDisk = (disk is null ? null : DiskOptions.FirstOrDefault(option => option.Source == disk.Source && option.Number == disk.Number)) ?? DiskOptions[0];
        TextWeight = weight;
        Language = language;
        InteractionKey = interactionKey;
        Monitor = monitor;
        Mode = mode;
        BorderMode = borderMode;
        Theme = theme;
        OnPropertyChanged(nameof(TextWeight));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(InteractionKey));
        OnPropertyChanged(nameof(Monitor));
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(BorderMode));
        OnPropertyChanged(nameof(Theme));
    }

    private void LoadGeneral(GeneralSettings general)
    {
        Language = LanguageOptions.Any(option => option.Code == general.Language) ? general.Language : Localizer.AutomaticLanguage;
        Theme = Enum.IsDefined(general.Theme) ? general.Theme : AppTheme.System;
        Mode = Enum.IsDefined(general.Mode) ? general.Mode : WidgetMode.AlwaysOnTop;
        SnapToEdges = general.SnapToEdges;
        PositionLocked = general.PositionLocked;
        ClickThrough = general.ClickThrough;
        InteractionKey = Enum.IsDefined(general.InteractionKey) ? general.InteractionKey : InteractionKey.Ctrl;
        StartWithWindows = general.StartWithWindows;
        Monitor = MonitorOptions.Any(option => option.Device == general.Monitor)
            ? general.Monitor
            : (MonitorOptions.FirstOrDefault(option => option.IsPrimary) ?? MonitorOptions.FirstOrDefault())?.Device;
    }

    private void LoadDisplay(DisplaySettings display)
    {
        for (int index = 0; index < display.Order.Items.Count; index++)
        {
            MetricRowViewModel row = _metricRows[display.Order.Items[index]];
            MoveMetric(row, index);
            row.IsVisible = display.IsShown(row.Item);
        }

        ShowFps = display.ShowFps;
        SelectedDisk = DiskOptions.FirstOrDefault(option => option.Matches(display)) ?? DiskOptions[0];
        DetailedTooltip = display.DetailedTooltip;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RefreshPending();

    private void OnMetricRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MetricRowViewModel.IsVisible))
        {
            return;
        }

        OnPropertyChanged(nameof(HasDisplayError));
    }
}

public enum SettingsTab
{
    General,
    Display,
    Appearance,
    Network,
}

public sealed record ModeOption(WidgetMode Mode, string Label);

public sealed record BorderModeOption(BorderMode Mode, string Label);

public sealed record ThemeOption(AppTheme Theme, string Label);

public sealed record MonitorOption(string Device, bool IsPrimary, string Label);

public sealed record DiskOption(DiskSource Source, int? Number, string Label)
{
    public bool Matches(DisplaySettings display) =>
        Source == display.DiskSource && (Source != DiskSource.Specific || Number == display.DiskNumber);
}
