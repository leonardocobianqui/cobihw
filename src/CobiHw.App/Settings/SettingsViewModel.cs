using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.App.Settings;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<MetricItem, MetricRowViewModel> _metricRows;
    private readonly IReadOnlyList<DiskActivity> _availableDisks;
    private readonly IReadOnlyList<DisplayMonitor> _monitors;
    private SavedSettings _applied;
    private AppearancePreview? _pendingAppearance;
    private IReadOnlyList<NetworkDeviceChoice> _absentDeviceChoices = [];
    private string? _fpsNoticeKey;
    private bool _ready;

    public SettingsViewModel(SavedSettings current, IReadOnlyList<DiskActivity> availableDisks, IReadOnlyList<DisplayMonitor> monitors, IReadOnlyList<NetworkDeviceInfo> networkDevices, SpeedTestViewModel speedTest)
    {
        SpeedTest = speedTest;
        NetworkDevices = [.. networkDevices.Select(device => new NetworkDeviceRowViewModel(device))];

        foreach (NetworkDeviceRowViewModel row in NetworkDevices)
        {
            row.PropertyChanged += OnNetworkDeviceRowPropertyChanged;
        }

        _metricRows = MetricOrder.Orderable.ToDictionary(item => item, item => new MetricRowViewModel(item));
        Metrics = [.. MetricOrder.Orderable.Select(item => _metricRows[item])];

        foreach (MetricRowViewModel row in Metrics)
        {
            row.PropertyChanged += OnMetricRowPropertyChanged;
        }

        _availableDisks = availableDisks;
        _diskOptions = BuildDiskOptions(availableDisks, current.Display);
        _languageOptions = Localizer.Languages();
        _monitors = monitors;
        _monitorOptions = BuildMonitorOptions(monitors);
        _modeOptions = BuildModeOptions();
        LoadGeneral(current.General);
        LoadDisplay(current.Display);
        LoadNetwork(current.Network);
        ShowInternalDevices = NetworkDevices.Any(row => row.IsInternal && row.IsMonitored);
        UpdateDeviceVisibility();
        Localizer.Instance.CultureChanged += OnCultureChanged;

        _applied = new SavedSettings(BuildDisplay(), current.Colors, BuildNetwork(), current.Appearance, BuildGeneral());
        _ready = true;
    }

    public event EventHandler<SavedSettings>? Applied;

    public event EventHandler<AppearanceGroup>? AppearanceEditRequested;

    public event EventHandler<AppearancePreview>? AppearancePreviewed;

    [ObservableProperty] private IReadOnlyList<DiskOption> _diskOptions;

    [ObservableProperty] private IReadOnlyList<LanguageOption> _languageOptions;

    [ObservableProperty] private IReadOnlyList<KeyOption> _keyOptions = BuildKeyOptions();

    [ObservableProperty] private IReadOnlyList<MonitorOption> _monitorOptions;

    [ObservableProperty] private IReadOnlyList<ModeOption> _modeOptions;

    [ObservableProperty] private IReadOnlyList<ThemeOption> _themeOptions = BuildThemeOptions();

    [ObservableProperty]
    private SettingsTab _selectedTab;

    public ObservableCollection<MetricRowViewModel> Metrics { get; }

    public ObservableCollection<NetworkDeviceRowViewModel> NetworkDevices { get; }

    public bool HasNoNetworkDevices => NetworkDevices.Count == 0;

    [ObservableProperty]
    private bool _showInternalDevices;

    [ObservableProperty]
    private IReadOnlyList<NetworkDefaultOption> _defaultDeviceOptions = [];

    [ObservableProperty]
    private NetworkDefaultOption? _selectedDefaultDevice;

    public AppearancePreview? PendingAppearance => _pendingAppearance;

    public AppearancePreview ShownAppearance => _pendingAppearance ?? new AppearancePreview(_applied.Appearance, _applied.Colors);

    public string ShapeSummary => AppearanceSummary.Shape(ShownAppearance.Appearance);

    public string TextColorsSummary => AppearanceSummary.TextColors(ShownAppearance.Appearance);

    public string UsageColorsSummary => AppearanceSummary.UsageColors(ShownAppearance.Colors);


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

    public string? FpsNotice => _fpsNoticeKey is null ? null : Localizer.Instance[_fpsNoticeKey];

    public bool HasFpsNotice => _fpsNoticeKey is not null;

    public bool IsGeneralPending => BuildGeneral() != _applied.General;

    public bool IsDisplayPending => BuildDisplay() != _applied.Display;

    public bool IsNetworkPending => BuildNetwork() != _applied.Network;

    public bool IsAppearancePending => _pendingAppearance is not null;

    public bool HasPendingChanges => IsGeneralPending || IsDisplayPending || IsAppearancePending || IsNetworkPending;

    public bool CanApplyAll => !HasDisplayError && !HasPingHostError && HasPendingChanges;

    public string PendingTabNames => string.Join(", ", new (bool Pending, string Name)[]
    {
        (IsGeneralPending, Strings.Tab_General),
        (IsDisplayPending, Strings.Tab_Display),
        (IsAppearancePending, Strings.Tab_Appearance),
        (IsNetworkPending, Strings.Tab_Network),
    }.Where(tab => tab.Pending).Select(tab => tab.Name));

    private bool CanApply() => SelectedTab switch
    {
        SettingsTab.Display => !HasDisplayError && IsDisplayPending,
        SettingsTab.Appearance => IsAppearancePending,
        SettingsTab.Network => !HasPingHostError && IsNetworkPending,
        _ => IsGeneralPending,
    };

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply() => Commit(SelectedTab switch
    {
        SettingsTab.Display => _applied with { Display = BuildDisplay() },
        SettingsTab.Appearance => _applied with { Appearance = ShownAppearance.Appearance, Colors = ShownAppearance.Colors },
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
    private void EditAppearance(AppearanceGroup group) => AppearanceEditRequested?.Invoke(this, group);

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
                break;
            case SettingsTab.Network:
                LoadNetwork(new NetworkSettings());
                break;
            default:
                LoadGeneral(new GeneralSettings());
                break;
        }
    }

    private void RestoreAppearance()
    {
        var defaults = new AppearancePreview(new AppearanceSettings(), new ColorSettings());
        _pendingAppearance = defaults == new AppearancePreview(_applied.Appearance, _applied.Colors) ? null : defaults;
        RefreshAppearance();
        AppearancePreviewed?.Invoke(this, ShownAppearance);
    }

    private void Commit(SavedSettings settings)
    {
        _applied = settings;

        if (_pendingAppearance == new AppearancePreview(settings.Appearance, settings.Colors))
        {
            _pendingAppearance = null;
        }

        Applied?.Invoke(this, settings);
        RefreshAppearance();
    }

    private SavedSettings BuildSettings() => new(BuildDisplay(), ShownAppearance.Colors, BuildNetwork(), ShownAppearance.Appearance, BuildGeneral());

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

    private NetworkSettings BuildNetwork() => new()
    {
        PingHost = PingHostText.Trim(),
        Devices = new NetworkDeviceChoices([
            .. _absentDeviceChoices,
            .. NetworkDevices
                .Where(row => row.IsMonitored == row.IsInternal)
                .Select(row => new NetworkDeviceChoice(row.Id, row.IsMonitored)),
        ]),
        DefaultDevice = SelectedDefaultDevice?.ToReference(),
    };

    private void LoadNetwork(NetworkSettings network)
    {
        PingHostText = network.PingHost;
        _absentDeviceChoices = [.. network.Devices.Overrides.Where(choice => NetworkDevices.All(row => row.Id != choice.Id))];

        foreach (NetworkDeviceRowViewModel row in NetworkDevices)
        {
            row.IsMonitored = network.Devices.IsMonitored(row.Id, row.IsInternal);
        }

        BuildDefaultDeviceOptions(network.DefaultDevice);
    }

    private void BuildDefaultDeviceOptions(NetworkDeviceRef? selected)
    {
        List<NetworkDefaultOption> options =
        [
            new NetworkDefaultOption(null, Strings.Network_PhysicalSum, Strings.Network_PhysicalSum),
            .. NetworkDevices.Where(row => row.IsMonitored).Select(row => new NetworkDefaultOption(row.Id, row.Name, row.Name)),
        ];

        if (selected is not null && NetworkDevices.All(row => row.Id != selected.Id))
        {
            options.Add(new NetworkDefaultOption(selected.Id, selected.Name, Localizer.Format(Strings.Format_NetworkDeviceMissing, selected.Name)));
        }

        DefaultDeviceOptions = options;
        SelectedDefaultDevice = options.FirstOrDefault(option => option.Id == selected?.Id) ?? options[0];
    }

    private void OnNetworkDeviceRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NetworkDeviceRowViewModel.IsMonitored))
        {
            BuildDefaultDeviceOptions(SelectedDefaultDevice?.ToReference());
        }
    }

    partial void OnShowInternalDevicesChanged(bool value) => UpdateDeviceVisibility();

    private void UpdateDeviceVisibility()
    {
        foreach (NetworkDeviceRowViewModel row in NetworkDevices)
        {
            row.IsShown = ShowInternalDevices || !row.IsInternal;
        }
    }

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
        _applied = _applied with { Appearance = _applied.Appearance with { Scale = percent } };

        if (_pendingAppearance is not null)
        {
            _pendingAppearance = _pendingAppearance with { Appearance = _pendingAppearance.Appearance with { Scale = percent } };
        }

        RefreshAppearance();
    }

    public void SyncAppearance(AppearanceSettings appearance, ColorSettings colors)
    {
        _applied = _applied with { Appearance = appearance, Colors = colors };
        _pendingAppearance = null;
        RefreshAppearance();
    }

    private void RefreshAppearance()
    {
        OnPropertyChanged(nameof(ShapeSummary));
        OnPropertyChanged(nameof(TextColorsSummary));
        OnPropertyChanged(nameof(UsageColorsSummary));
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
        foreach (MetricRowViewModel row in Metrics)
        {
            row.RefreshName();
        }

        foreach (NetworkDeviceRowViewModel row in NetworkDevices)
        {
            row.RefreshTexts();
        }

        OnPropertyChanged(nameof(FpsNotice));
        OnPropertyChanged(nameof(ShapeSummary));
        OnPropertyChanged(nameof(TextColorsSummary));
        OnPropertyChanged(nameof(UsageColorsSummary));

        DiskOption? disk = SelectedDisk;
        string language = Language;
        InteractionKey interactionKey = InteractionKey;
        string? monitor = Monitor;
        WidgetMode mode = Mode;
        AppTheme theme = Theme;

        DiskOptions = BuildDiskOptions(_availableDisks, _applied.Display);
        BuildDefaultDeviceOptions(SelectedDefaultDevice?.ToReference());
        LanguageOptions = Localizer.Languages();
        KeyOptions = BuildKeyOptions();
        MonitorOptions = BuildMonitorOptions(_monitors);
        ModeOptions = BuildModeOptions();
        ThemeOptions = BuildThemeOptions();

        SelectedDisk = (disk is null ? null : DiskOptions.FirstOrDefault(option => option.Source == disk.Source && option.Number == disk.Number)) ?? DiskOptions[0];
        Language = language;
        InteractionKey = interactionKey;
        Monitor = monitor;
        Mode = mode;
        Theme = theme;
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(InteractionKey));
        OnPropertyChanged(nameof(Monitor));
        OnPropertyChanged(nameof(Mode));
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

public sealed record ThemeOption(AppTheme Theme, string Label);

public sealed record MonitorOption(string Device, bool IsPrimary, string Label);

public sealed record DiskOption(DiskSource Source, int? Number, string Label)
{
    public bool Matches(DisplaySettings display) =>
        Source == display.DiskSource && (Source != DiskSource.Specific || Number == display.DiskNumber);
}
