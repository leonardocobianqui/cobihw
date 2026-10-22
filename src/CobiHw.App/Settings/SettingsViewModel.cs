using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.App.Settings;

/// <summary>
/// State of the settings window: the values being edited in each tab and what has already been applied.
/// </summary>
/// <remarks>
/// Edits stay local until "Apply" (current tab) or "Apply all" on close; <see cref="Applied"/> then hands the
/// result to the app. Appearance edits happen in a separate editor and arrive as a pending preview.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<MetricItem, MetricRowViewModel> _metricRows;
    private readonly IReadOnlyList<DiskActivity> _availableDisks;
    private readonly IReadOnlyList<GpuActivity> _availableGpus;
    private readonly IReadOnlyList<DisplayMonitor> _monitors;
    // Last values handed to the app; "pending" means the edited values differ from these.
    private SavedSettings _applied;
    // Appearance changed in the editor but not applied yet; null when there is nothing pending.
    private AppearancePreview? _pendingAppearance;
    // Choices for devices not present right now (e.g. a USB adapter unplugged), kept so they aren't lost on save.
    private IReadOnlyList<NetworkDeviceChoice> _absentDeviceChoices = [];
    private string? _fpsNoticeKey;
    // False during construction, so loading the initial values doesn't raise "pending" updates.
    private bool _ready;

    /// <summary>Loads the current settings and the hardware found in the last reading.</summary>
    /// <param name="current">Settings in effect.</param>
    /// <param name="availableDisks">Disks for the "monitored disk" box.</param>
    /// <param name="availableGpus">GPUs for the "monitored GPU" box.</param>
    /// <param name="hasBattery">False hides the Battery row.</param>
    /// <param name="monitors">Monitors for the "monitor" box.</param>
    /// <param name="networkDevices">Devices for the Network tab.</param>
    /// <param name="speedTest">Shared speed test state.</param>
    public SettingsViewModel(SavedSettings current, IReadOnlyList<DiskActivity> availableDisks, IReadOnlyList<GpuActivity> availableGpus, bool hasBattery, IReadOnlyList<DisplayMonitor> monitors, IReadOnlyList<NetworkDeviceInfo> networkDevices, SpeedTestViewModel speedTest)
    {
        SpeedTest = speedTest;
        NetworkDevices = [.. networkDevices.Select(device => new NetworkDeviceRowViewModel(device))];

        foreach (NetworkDeviceRowViewModel row in NetworkDevices)
        {
            row.PropertyChanged += OnNetworkDeviceRowPropertyChanged;
        }

        _metricRows = MetricOrder.Orderable.ToDictionary(item => item, item => new MetricRowViewModel(item));
        _metricRows[MetricItem.Battery].IsAvailable = hasBattery;
        Metrics = [.. MetricOrder.Orderable.Select(item => _metricRows[item])];

        foreach (MetricRowViewModel row in Metrics)
        {
            row.PropertyChanged += OnMetricRowPropertyChanged;
        }

        _availableDisks = availableDisks;
        _diskOptions = BuildDiskOptions(availableDisks, current.Display);
        _availableGpus = availableGpus;
        _gpuOptions = BuildGpuOptions(availableGpus, current.Display);
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

    /// <summary>Raised with the full settings whenever a tab is applied.</summary>
    public event EventHandler<SavedSettings>? Applied;

    /// <summary>Asks the app to open the appearance editor for a group.</summary>
    public event EventHandler<AppearanceGroup>? AppearanceEditRequested;

    /// <summary>Asks the app to preview an appearance on the widget without saving it.</summary>
    public event EventHandler<AppearancePreview>? AppearancePreviewed;

    /// <summary>Asks the app to confirm and remove other computers' profiles.</summary>
    public event EventHandler? RemoveOtherProfilesRequested;

    /// <summary>Asks the app to confirm and remove the startup entry and FPS task.</summary>
    public event EventHandler? RemoveFromWindowsRequested;

    /// <summary>Enables "Remove other sections" when the file has profiles of other computers or users.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveOtherProfilesCommand))]
    private bool _hasOtherProfiles;

    /// <summary>Options of the "monitored disk" box (rebuilt when the language changes).</summary>
    [ObservableProperty] private IReadOnlyList<DiskOption> _diskOptions;

    /// <summary>Options of the "monitored GPU" box.</summary>
    [ObservableProperty] private IReadOnlyList<GpuOption> _gpuOptions;

    /// <summary>Options of the language box.</summary>
    [ObservableProperty] private IReadOnlyList<LanguageOption> _languageOptions;

    /// <summary>Options of the interaction key box.</summary>
    [ObservableProperty] private IReadOnlyList<KeyOption> _keyOptions = BuildKeyOptions();

    /// <summary>Options of the monitor box.</summary>
    [ObservableProperty] private IReadOnlyList<MonitorOption> _monitorOptions;

    /// <summary>Options of the widget mode box.</summary>
    [ObservableProperty] private IReadOnlyList<ModeOption> _modeOptions;

    /// <summary>Options of the theme box.</summary>
    [ObservableProperty] private IReadOnlyList<ThemeOption> _themeOptions = BuildThemeOptions();

    /// <summary>Open tab; "Apply" and "Restore defaults" act on it.</summary>
    [ObservableProperty]
    private SettingsTab _selectedTab;

    /// <summary>Widget items in display order; dragging a row reorders them.</summary>
    public ObservableCollection<MetricRowViewModel> Metrics { get; }

    /// <summary>Network devices found when the window opened.</summary>
    public ObservableCollection<NetworkDeviceRowViewModel> NetworkDevices { get; }

    /// <summary>Shows the "no devices" message.</summary>
    public bool HasNoNetworkDevices => NetworkDevices.Count == 0;

    /// <summary>Shows devices Windows hides (filters, virtual adapters).</summary>
    [ObservableProperty]
    private bool _showInternalDevices;

    /// <summary>Options of the "default device" box: the sum plus each checked device.</summary>
    [ObservableProperty]
    private IReadOnlyList<NetworkDefaultOption> _defaultDeviceOptions = [];

    /// <summary>Device shown in the widget.</summary>
    [ObservableProperty]
    private NetworkDefaultOption? _selectedDefaultDevice;

    /// <summary>Appearance edited but not applied, if any.</summary>
    public AppearancePreview? PendingAppearance => _pendingAppearance;

    /// <summary>Appearance currently on screen: the pending one, or the applied one.</summary>
    public AppearancePreview ShownAppearance => _pendingAppearance ?? new AppearancePreview(_applied.Appearance, _applied.Colors);

    /// <summary>Summary line under "Shape".</summary>
    public string ShapeSummary => AppearanceSummary.Shape(ShownAppearance.Appearance);

    /// <summary>Summary line under "Colors and text".</summary>
    public string TextColorsSummary => AppearanceSummary.TextColors(ShownAppearance.Appearance);

    /// <summary>Summary line under "Usage colors".</summary>
    public string UsageColorsSummary => AppearanceSummary.UsageColors(ShownAppearance.Colors);


    /// <summary>Suggested ping hosts in the address box.</summary>
    public IReadOnlyList<string> PingSuggestions { get; } = [NetworkSettings.GooglePingHost, NetworkSettings.CloudflarePingHost];

    /// <summary>Shared speed test state, also used by the widget and tray.</summary>
    public SpeedTestViewModel SpeedTest { get; }

    /// <summary>Disk shown in the widget.</summary>
    [ObservableProperty]
    private DiskOption? _selectedDisk;

    /// <summary>"Show read and write speed" checkbox.</summary>
    [ObservableProperty]
    private bool _showDiskSpeed;

    /// <summary>GPU shown in the widget.</summary>
    [ObservableProperty]
    private GpuOption? _selectedGpu;

    /// <summary>"Show temperature" checkbox.</summary>
    [ObservableProperty]
    private bool _showGpuTemperature;

    /// <summary>"FPS" checkbox; turning it on asks for administrator rights.</summary>
    [ObservableProperty]
    private bool _showFps;

    /// <summary>"Show details on mouse hover" checkbox.</summary>
    [ObservableProperty]
    private bool _detailedTooltip;

    /// <summary>"Ignore clicks" checkbox.</summary>
    [ObservableProperty]
    private bool _clickThrough;

    /// <summary>Key that makes the widget clickable while held.</summary>
    [ObservableProperty]
    private InteractionKey _interactionKey;

    /// <summary>Language code or "auto".</summary>
    [ObservableProperty]
    private string _language = Localizer.AutomaticLanguage;

    /// <summary>Theme of this window and the tray menu.</summary>
    [ObservableProperty]
    private AppTheme _theme;

    /// <summary>Device name of the monitor the widget sits on.</summary>
    [ObservableProperty]
    private string? _monitor;

    /// <summary>Always on top or on the desktop.</summary>
    [ObservableProperty]
    private WidgetMode _mode;

    /// <summary>Snap to screen edges while dragging.</summary>
    [ObservableProperty]
    private bool _snapToEdges;

    /// <summary>Lock position and size.</summary>
    [ObservableProperty]
    private bool _positionLocked;

    /// <summary>Start with Windows.</summary>
    [ObservableProperty]
    private bool _startWithWindows;

    /// <summary>Ping address as typed; validated before applying.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPingHostError))]
    private string _pingHostText = string.Empty;

    /// <summary>"Measure ping" checkbox.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPingHostError))]
    private bool _pingEnabled;

    /// <summary>True when no available item is checked (the widget would be empty).</summary>
    public bool HasDisplayError => !Metrics.Any(row => row.IsVisible && row.IsAvailable);

    /// <summary>True when ping is on and the address isn't a valid IP or host name.</summary>
    public bool HasPingHostError => PingEnabled && Uri.CheckHostName(PingHostText.Trim()) == UriHostNameType.Unknown;

    /// <summary>Shows the monitor box only when there is a choice.</summary>
    public bool HasSeveralMonitors => _monitors.Count > 1;

    /// <summary>Message under the FPS checkbox (e.g. administrator permission denied).</summary>
    public string? FpsNotice => _fpsNoticeKey is null ? null : Localizer.Instance[_fpsNoticeKey];

    /// <summary>Shows the FPS message.</summary>
    public bool HasFpsNotice => _fpsNoticeKey is not null;

    /// <summary>General tab has changes not applied.</summary>
    public bool IsGeneralPending => BuildGeneral() != _applied.General;

    /// <summary>Display tab has changes not applied.</summary>
    public bool IsDisplayPending => BuildDisplay() != _applied.Display;

    /// <summary>Network tab has changes not applied.</summary>
    public bool IsNetworkPending => BuildNetwork() != _applied.Network;

    /// <summary>Appearance tab has changes not applied.</summary>
    public bool IsAppearancePending => _pendingAppearance is not null;

    /// <summary>Any tab has changes not applied; asked when the window closes.</summary>
    public bool HasPendingChanges => IsGeneralPending || IsDisplayPending || IsAppearancePending || IsNetworkPending;

    /// <summary>Every pending tab is valid, so "apply before closing" is possible.</summary>
    public bool CanApplyAll => !HasDisplayError && !HasPingHostError && HasPendingChanges;

    /// <summary>Names of the tabs with pending changes, for the close confirmation.</summary>
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

    // "Apply" sends only the open tab; the other tabs keep their applied values.
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply() => Commit(SelectedTab switch
    {
        SettingsTab.Display => _applied with { Display = BuildDisplay() },
        SettingsTab.Appearance => _applied with { Appearance = ShownAppearance.Appearance, Colors = ShownAppearance.Colors },
        SettingsTab.Network => _applied with { Network = BuildNetwork() },
        _ => _applied with { General = BuildGeneral() },
    });

    /// <summary>Applies every tab at once (used when closing with "Apply").</summary>
    public void ApplyAll()
    {
        if (CanApplyAll)
        {
            Commit(BuildSettings());
        }
    }

    /// <summary>Moves an item to a new position in the widget order.</summary>
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
    private void RemoveFromWindows() => RemoveFromWindowsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(HasOtherProfiles))]
    private void RemoveOtherProfiles() => RemoveOtherProfilesRequested?.Invoke(this, EventArgs.Empty);

    // Restores the defaults of the open tab only.
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

    // Defaults are previewed like any edit; equal to the applied values means nothing is pending.
    private void RestoreAppearance()
    {
        var defaults = new AppearancePreview(new AppearanceSettings(), new ColorSettings());
        _pendingAppearance = defaults == new AppearancePreview(_applied.Appearance, _applied.Colors) ? null : defaults;
        RefreshAppearance();
        AppearancePreviewed?.Invoke(this, ShownAppearance);
    }

    // Records the new applied state and hands it to the app.
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
        ShowBattery = _metricRows[MetricItem.Battery].IsVisible,
        ShowFps = ShowFps,
        Order = new MetricOrder([.. Metrics.Select(row => row.Item)]),
        DiskSource = SelectedDisk?.Source ?? DiskSource.System,
        DiskNumber = SelectedDisk?.Number,
        ShowDiskSpeed = ShowDiskSpeed,
        Gpu = SelectedGpu?.Choice,
        ShowGpuTemperature = ShowGpuTemperature,
        DetailedTooltip = DetailedTooltip,
    };

    private NetworkSettings BuildNetwork() => new()
    {
        PingEnabled = PingEnabled,
        PingHost = PingHostText.Trim(),
        Devices = new NetworkDeviceChoices([
            .. _absentDeviceChoices,
            .. NetworkDevices
                // Saves only choices that differ from the default (visible = checked).
                .Where(row => row.IsMonitored == row.IsInternal)
                .Select(row => new NetworkDeviceChoice(row.Id, row.IsMonitored)),
        ]),
        DefaultDevice = SelectedDefaultDevice?.ToReference(),
    };

    private void LoadNetwork(NetworkSettings network)
    {
        PingEnabled = network.PingEnabled;
        PingHostText = network.PingHost;
        _absentDeviceChoices = [.. network.Devices.Overrides.Where(choice => NetworkDevices.All(row => row.Id != choice.Id))];

        foreach (NetworkDeviceRowViewModel row in NetworkDevices)
        {
            row.IsMonitored = network.Devices.IsMonitored(row.Id, row.IsInternal);
        }

        BuildDefaultDeviceOptions(network.DefaultDevice);
    }

    // A saved default device that is gone stays selectable as "(not found)", so it isn't silently replaced.
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

    // Windows disk first, then the others; a saved disk that is gone is kept as "(not found)".
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

    // "Automatic" first (named after the GPU it picks), then each GPU in Windows' order.
    private static List<GpuOption> BuildGpuOptions(IReadOnlyList<GpuActivity> availableGpus, DisplaySettings saved)
    {
        GpuActivity? preferred = availableGpus.MinBy(gpu => gpu.Rank);

        List<GpuOption> options =
        [
            new GpuOption(null, preferred is null ? Strings.Gpu_Automatic : Localizer.Format(Strings.Format_GpuAutomatic, preferred.Name)),
        ];

        options.AddRange(availableGpus
            .OrderBy(gpu => gpu.Rank)
            .Select(gpu => new GpuOption(GpuChoice.Of(gpu), GpuLabel(gpu, availableGpus))));

        if (saved.Gpu is GpuChoice missing && availableGpus.All(gpu => gpu.Id != missing.Id))
        {
            options.Add(new GpuOption(missing, Localizer.Format(Strings.Format_GpuOptionNotFound, missing.Name)));
        }

        return options;
    }

    // Identical cards get "(1)", "(2)" so they can be told apart.
    private static string GpuLabel(GpuActivity gpu, IReadOnlyList<GpuActivity> availableGpus) =>
        availableGpus.Count(other => other.Id.VendorId == gpu.Id.VendorId && other.Id.DeviceId == gpu.Id.DeviceId) > 1
            ? Localizer.Format("{0} ({1})", gpu.Name, gpu.Id.Instance + 1)
            : gpu.Name;

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

    /// <summary>Reflects General values changed outside this window (tray menu, dragging) without marking them pending.</summary>
    /// <remarks>Fields the user is editing here keep their edited value.</remarks>
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

    /// <summary>Turns FPS back off because enabling it failed, and shows why.</summary>
    public void RejectFps(string noticeKey)
    {
        ShowFps = false;
        _applied = _applied with { Display = _applied.Display with { ShowFps = false } };
        ShowFpsNotice(noticeKey);
    }

    /// <summary>Reflects FPS turned off by the app ("Remove from Windows") without marking it pending.</summary>
    public void SyncFpsOff()
    {
        ShowFps = false;
        _applied = _applied with { Display = _applied.Display with { ShowFps = false } };
        RefreshPending();
    }

    /// <summary>Shows a message under the FPS checkbox.</summary>
    public void ShowFpsNotice(string noticeKey)
    {
        _fpsNoticeKey = noticeKey;
        OnPropertyChanged(nameof(FpsNotice));
        OnPropertyChanged(nameof(HasFpsNotice));
    }

    /// <summary>Reflects a size change made by dragging the widget edges.</summary>
    public void SyncScale(int percent)
    {
        _applied = _applied with { Appearance = _applied.Appearance with { Scale = percent } };

        if (_pendingAppearance is not null)
        {
            _pendingAppearance = _pendingAppearance with { Appearance = _pendingAppearance.Appearance with { Scale = percent } };
        }

        RefreshAppearance();
    }

    /// <summary>Reflects an appearance applied from the editor.</summary>
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

    /// <summary>Stops listening for language changes.</summary>
    public void Dispose() => Localizer.Instance.CultureChanged -= OnCultureChanged;

    partial void OnShowFpsChanged(bool value)
    {
        _fpsNoticeKey = null;
        OnPropertyChanged(nameof(FpsNotice));
        OnPropertyChanged(nameof(HasFpsNotice));
    }

    // Any edited value may change what's pending, so every property change refreshes it.
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

    // Option lists hold translated labels, so they are rebuilt and the selections restored.
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
        GpuOption? gpu = SelectedGpu;
        string language = Language;
        InteractionKey interactionKey = InteractionKey;
        string? monitor = Monitor;
        WidgetMode mode = Mode;
        AppTheme theme = Theme;

        DiskOptions = BuildDiskOptions(_availableDisks, _applied.Display);
        GpuOptions = BuildGpuOptions(_availableGpus, _applied.Display);
        BuildDefaultDeviceOptions(SelectedDefaultDevice?.ToReference());
        LanguageOptions = Localizer.Languages();
        KeyOptions = BuildKeyOptions();
        MonitorOptions = BuildMonitorOptions(_monitors);
        ModeOptions = BuildModeOptions();
        ThemeOptions = BuildThemeOptions();

        SelectedDisk = (disk is null ? null : DiskOptions.FirstOrDefault(option => option.Source == disk.Source && option.Number == disk.Number)) ?? DiskOptions[0];
        SelectedGpu = GpuOptions.FirstOrDefault(option => option.Choice?.Id == gpu?.Choice?.Id) ?? GpuOptions[0];
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

    // Invalid saved values (unknown language, removed monitor) fall back to safe defaults.
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
        ShowDiskSpeed = display.ShowDiskSpeed;
        SelectedGpu = GpuOptions.FirstOrDefault(option => option.Matches(display)) ?? GpuOptions[0];
        ShowGpuTemperature = display.ShowGpuTemperature;
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

/// <summary>Tabs of the settings window.</summary>
public enum SettingsTab
{
    General,
    Display,
    Appearance,
    Network,
}

/// <summary>One option of the widget mode box.</summary>
/// <param name="Mode">Mode value.</param>
/// <param name="Label">Translated name.</param>
public sealed record ModeOption(WidgetMode Mode, string Label);

/// <summary>One option of the theme box.</summary>
/// <param name="Theme">Theme value.</param>
/// <param name="Label">Translated name.</param>
public sealed record ThemeOption(AppTheme Theme, string Label);

/// <summary>One option of the monitor box.</summary>
/// <param name="Device">Device name, e.g. "\\.\DISPLAY1".</param>
/// <param name="IsPrimary">True for the primary monitor.</param>
/// <param name="Label">"Monitor 1 — 1920 × 1080 (primary)".</param>
public sealed record MonitorOption(string Device, bool IsPrimary, string Label);

/// <summary>One option of the monitored disk box.</summary>
/// <param name="Source">Windows disk or a specific one.</param>
/// <param name="Number">Disk number for a specific disk.</param>
/// <param name="Label">Text shown in the box.</param>
public sealed record DiskOption(DiskSource Source, int? Number, string Label)
{
    /// <summary>True when this option is the saved choice.</summary>
    public bool Matches(DisplaySettings display) =>
        Source == display.DiskSource && (Source != DiskSource.Specific || Number == display.DiskNumber);
}

/// <summary>One option of the monitored GPU box.</summary>
/// <param name="Choice">GPU to save; null = automatic.</param>
/// <param name="Label">Text shown in the box.</param>
public sealed record GpuOption(GpuChoice? Choice, string Label)
{
    /// <summary>True when this option is the saved choice.</summary>
    public bool Matches(DisplaySettings display) => Choice?.Id == display.Gpu?.Id;
}
