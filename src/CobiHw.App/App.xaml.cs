using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.About;
using Microsoft.Win32;
using CobiHw.App.Devices;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using CobiHw.App.Tray;
using CobiHw.Core;
using CobiHw.Core.Fps;
using CobiHw.Core.Network;
using Forms = System.Windows.Forms;

namespace CobiHw.App;

/// <summary>
/// Application entry: loads the settings, creates the widget, the tray icon and the background loops, and connects every window.
/// Runs as a single instance per user session; launching it again just shows the widget.
/// </summary>
public partial class App : Application
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(1);
    // How long to wait for a settings file another process is writing.
    private static readonly TimeSpan SettingsLoadPatience = TimeSpan.FromSeconds(5);
    // While the file stays busy, checks it again at this interval.
    private static readonly TimeSpan SettingsRetryInterval = TimeSpan.FromSeconds(5);
    // The last save on exit waits at most this long for a busy file.
    private static readonly TimeSpan ExitSavePatience = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(2);
    // Long enough for the speed test transfers.
    private static readonly TimeSpan HttpRequestTimeout = TimeSpan.FromSeconds(60);
    private static readonly Uri IconResource = new("pack://application:,,,/Assets/app.ico");
    // ERROR_CANCELLED: the user declined the UAC prompt.
    private const int ErrorCancelled = 1223;

    private readonly CancellationTokenSource _shutdown = new();
    private SingleInstance? _singleInstance;
    private SettingsStore? _settingsStore;
    private AppSettings _settings = new();
    private HardwareMonitor? _monitor;
    private HttpClient? _http;
    private SpeedTestViewModel? _speedTest;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private TrayIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private SettingsViewModel? _settingsViewModel;
    private NetworkDevicesWindow? _networkDevicesWindow;
    private AboutWindow? _aboutWindow;
    private SpeedTestWindow? _speedTestWindow;
    private NetworkDevicesViewModel? _networkDevicesViewModel;
    // Blocks a second open while the device list is loading.
    private bool _openingSettings;
    // Only the first unhandled error is shown.
    private bool _showingError;
    private DispatcherTimer? _autoSaveTimer;
    private FpsMonitor? _fps;
    // True while the UAC prompt is open.
    private bool _fpsPermissionPending;
    // Read by OnExit: restart elevated (FPS), or restart to load a settings file that was busy.
    private bool _relaunchElevated;
    private bool _relaunchAfterLoad;
    private string[] _startupArguments = [];
    private DispatcherTimer? _settingsRetryTimer;

    // Path of the running .exe; the scheduled task and the startup entry point to it.
    private static string ExecutablePath => Environment.ProcessPath
        ?? throw new InvalidOperationException("The executable path is not available.");

    /// <summary>Starts the app: settings, widget, tray icon and background loops.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _startupArguments = e.Args;
        string? settingsPath = ElevatedTask.SettingsPathFrom(e.Args);

        // Elevated helper run: only registers the FPS task and exits with the result.
        if (e.Args.Contains(ElevatedTask.RegisterArgument))
        {
            Shutdown(settingsPath is not null && RegisterTask(settingsPath) ? 0 : 1);
            return;
        }

        ProfileIdentity identity = ProfileIdentity.Current();
        _settingsStore = settingsPath is null ? SettingsStore.CreateBesideExecutable(identity) : SettingsStore.At(settingsPath, identity);
        _settings = _settingsStore.Load(SettingsLoadPatience);

        // With FPS on, a normal launch hands over to the elevated scheduled task (no UAC prompt) and exits.
        if (settingsPath is null && CanRelaunchElevated() && ElevatedTask.Run())
        {
            Shutdown();
            return;
        }

        // Another instance is running and was asked to show its widget.
        _singleInstance = SingleInstance.TryAcquire();
        if (_singleInstance is null)
        {
            Shutdown();
            return;
        }

        Localizer.Instance.SetLanguage(_settings.Language);
        Localizer.Instance.CultureChanged += (_, _) => _trayIcon?.ApplyTexts();
        StartupRegistration.RepairIfMoved();

        _monitor = new HardwareMonitor(_settings.NetworkSession);
        _http = CreateHttpClient();
        _speedTest = new SpeedTestViewModel(new CloudflareSpeedTest(_http), _settings.LastSpeedTest);
        _speedTest.Completed += (_, result) => OnSpeedTestCompleted(result);
        _speedTest.Failed += (_, message) => OnSpeedTestFailed(message);
        _speedTest.RunCommand.PropertyChanged += OnSpeedTestCommandPropertyChanged;
        _viewModel = new MainViewModel(CurrentSettings(), _speedTest) { Layout = _settings.Layout };

        _window = new MainWindow
        {
            DataContext = _viewModel,
            IsPositionLocked = _settings.PositionLocked,
            SnapToEdges = _settings.SnapToEdges,
            ScalePercent = ResizeRule.ClampPercent(_settings.Appearance.Scale),
        };
        _window.RestorePosition(_settings.WindowX, _settings.WindowY, _settings.WindowLeft, _settings.WindowTop);
        _window.ApplyMode(_settings.Mode);
        _window.ApplyClickThrough(_settings.ClickThrough, _settings.InteractionKey);
        _window.DragCompleted += (_, _) => AfterWindowMoved();
        _window.LayoutCycleRequested += (_, _) => ChangeLayout(NextLayout(_settings.Layout));
        _window.ScaleDragged += (_, percent) => _viewModel?.ApplyScale(percent);
        _window.ScaleCompleted += (_, percent) => ChangeScale(percent);
        MainWindow = _window;
        _window.Show();

        _trayIcon = new TrayIcon(IconResource);
        _trayIcon.ToggleVisibilityRequested += (_, _) => ToggleWindowVisibility();
        _trayIcon.ModeSelected += (_, mode) => ChangeMode(mode);
        _trayIcon.LayoutSelected += (_, layout) => ChangeLayout(layout);
        _trayIcon.SnapToEdgesToggled += (_, enabled) => ChangeSnapToEdges(enabled);
        _trayIcon.LockPositionToggled += (_, locked) => ChangePositionLock(locked);
        _trayIcon.ClickThroughToggled += (_, enabled) => ChangeClickThrough(enabled);
        _trayIcon.SettingsRequested += (_, _) => OpenSettingsWindow();
        _trayIcon.AboutRequested += (_, _) => OpenAboutWindow();
        _trayIcon.NetworkDevicesRequested += (_, _) => OpenNetworkDevicesWindow();
        _trayIcon.SpeedTestRequested += (_, _) => StartSpeedTest();
        _trayIcon.ResetNetworkRequested += (_, _) => ResetNetworkSession();
        _trayIcon.ExitRequested += (_, _) => RequestExit();
        RefreshTrayMenu();
        ApplyTrayTheme();
        _trayIcon.SetTooltip(_viewModel.TrayText);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _singleInstance.ListenForShowRequests(() => Dispatcher.InvokeAsync(ShowWindow));

        _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveInterval };
        _autoSaveTimer.Tick += (_, _) => SaveSettings();
        _autoSaveTimer.Start();

        // The file is busy: runs with defaults and relaunches once it can be read.
        if (_settingsStore.Access == SettingsAccess.FileInUse)
        {
            WatchSettingsFile();
        }

        if (_settings.Display.ShowFps)
        {
            EnableFps();
        }

        RunSamplingLoop(_monitor, _viewModel, _shutdown.Token);
        RunPingLoop(_viewModel, _shutdown.Token);

        if (_settingsStore.Access == SettingsAccess.FolderNotWritable)
        {
            MessageBox.Show(Strings.Settings_FolderReadOnly, TrayTooltip.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Stops the loops, saves the settings, releases resources and relaunches if requested.</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _speedTest?.RunCancelCommand.Execute(null);
        _shutdown.Cancel();
        _autoSaveTimer?.Stop();
        _settingsRetryTimer?.Stop();
        SaveSettings(ExitSavePatience);
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _trayIcon?.Dispose();
        _fps?.Dispose();
        _monitor?.Dispose();
        _http?.Dispose();
        _singleInstance?.Dispose();
        _shutdown.Dispose();

        // Runs after the single-instance lock is released, so the new process can take it.
        if (_relaunchElevated)
        {
            ElevatedTask.Run();
        }
        else if (_relaunchAfterLoad)
        {
            Relaunch();
        }

        base.OnExit(e);
    }

    // Runs in the elevated helper; the language is loaded so the task description is translated.
    private static bool RegisterTask(string settingsPath)
    {
        Localizer.Instance.SetLanguage(SettingsStore.At(settingsPath, ProfileIdentity.Current()).Load().Language);
        return ElevatedTask.Register(ExecutablePath, settingsPath);
    }

    private void WatchSettingsFile()
    {
        _settingsRetryTimer = new DispatcherTimer { Interval = SettingsRetryInterval };
        _settingsRetryTimer.Tick += (_, _) => RetrySettingsFile();
        _settingsRetryTimer.Start();
    }

    // Waits for the settings window to close, so pending edits aren't lost.
    private void RetrySettingsFile()
    {
        if (_settingsStore is null || _settingsWindow is not null || !_settingsStore.CanRead())
        {
            return;
        }

        _settingsRetryTimer?.Stop();
        _relaunchAfterLoad = true;
        RequestExit();
    }

    // Starts a new copy with the same arguments.
    private void Relaunch()
    {
        var startInfo = new ProcessStartInfo(ExecutablePath);

        foreach (string argument in _startupArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process.Start(startInfo)?.Dispose();
    }

    // One shared client for the speed test, the update check and the public IP.
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = HttpRequestTimeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("cobihw");
        return http;
    }

    // Double-click order: full → compact horizontal → compact vertical.
    private static ViewLayout NextLayout(ViewLayout current) => current switch
    {
        ViewLayout.Full => ViewLayout.CompactHorizontal,
        ViewLayout.CompactHorizontal => ViewLayout.CompactVertical,
        _ => ViewLayout.Full,
    };

    private void ToggleWindowVisibility()
    {
        if (_window is null)
        {
            return;
        }

        if (_window.IsVisible)
        {
            _window.Hide();
        }
        else
        {
            ShowWindow();
        }
    }

    private void ShowWindow() => _window?.Show();

    // Closes the other windows first; if the settings window cancels its close (pending changes), the app keeps running.
    private void RequestExit()
    {
        _networkDevicesWindow?.Close();
        _aboutWindow?.Close();
        _speedTestWindow?.Close();
        _settingsWindow?.Close();

        if (_settingsWindow is null)
        {
            Shutdown();
        }
    }

    // Saves the new position and updates the monitor box in the settings window.
    private void AfterWindowMoved()
    {
        SaveSettings();
        _settingsViewModel?.SyncGeneral(CurrentGeneral());
    }

    // After a change from the tray menu: refreshes the menu and the settings window, then saves.
    private void AfterGeneralChange()
    {
        RefreshTrayMenu();
        _settingsViewModel?.SyncGeneral(CurrentGeneral());
        SaveSettings();
    }

    // Change* methods come from the tray menu; Set* methods only apply the value.
    private void ChangeMode(WidgetMode mode)
    {
        SetMode(mode);
        AfterGeneralChange();
    }

    private void SetMode(WidgetMode mode)
    {
        _settings.Mode = mode;
        _window?.ApplyMode(mode);
    }

    private void ChangeLayout(ViewLayout layout)
    {
        _settings.Layout = layout;

        if (_viewModel is not null)
        {
            _viewModel.Layout = layout;
        }

        RefreshTrayMenu();
        SaveSettings();
    }

    private void ChangeSnapToEdges(bool enabled)
    {
        SetSnapToEdges(enabled);
        AfterGeneralChange();
    }

    private void SetSnapToEdges(bool enabled)
    {
        _settings.SnapToEdges = enabled;

        if (_window is not null)
        {
            _window.SnapToEdges = enabled;
        }
    }

    private void ChangePositionLock(bool locked)
    {
        SetPositionLock(locked);
        AfterGeneralChange();
    }

    private void SetPositionLock(bool locked)
    {
        _settings.PositionLocked = locked;

        if (_window is not null)
        {
            _window.IsPositionLocked = locked;
        }
    }

    private void ChangeClickThrough(bool enabled)
    {
        SetClickThrough(enabled, _settings.InteractionKey);
        AfterGeneralChange();
    }

    private void SetClickThrough(bool enabled, InteractionKey interactionKey)
    {
        _settings.ClickThrough = enabled;
        _settings.InteractionKey = interactionKey;
        _window?.ApplyClickThrough(enabled, interactionKey);
    }

    private void ChangeScale(int percent)
    {
        _settings.Appearance = _settings.Appearance with { Scale = percent };
        _settingsViewModel?.SyncScale(percent);
        SaveSettings();
    }

    private static void SetStartWithWindows(bool enabled)
    {
        if (enabled == StartupRegistration.IsEnabled())
        {
            return;
        }

        if (enabled)
        {
            StartupRegistration.Enable();
        }
        else
        {
            StartupRegistration.Disable();
        }
    }

    // Network devices are listed on a background thread; the window opens when they arrive.
    private async void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        if (_openingSettings || _viewModel is null || _speedTest is null)
        {
            return;
        }

        IReadOnlyList<NetworkDeviceInfo> networkDevices;
        _openingSettings = true;

        try
        {
            networkDevices = await Task.Run(ListNetworkDevices);
        }
        finally
        {
            _openingSettings = false;
        }

        if (_settingsWindow is not null || _viewModel is null || _speedTest is null)
        {
            return;
        }

        var settingsViewModel = new SettingsViewModel(CurrentSettings(), _viewModel.AvailableDisks, _viewModel.AvailableGpus, _viewModel.HasBattery, ListMonitors(), networkDevices, _speedTest);
        _settingsViewModel = settingsViewModel;
        settingsViewModel.Applied += (_, saved) => ApplySavedSettings(saved);
        settingsViewModel.AppearanceEditRequested += (_, group) => EditAppearance(group);
        settingsViewModel.AppearancePreviewed += (_, preview) => PreviewAppearance(preview);
        settingsViewModel.RemoveOtherProfilesRequested += (_, _) => RemoveOtherProfiles();
        settingsViewModel.RemoveFromWindowsRequested += (_, _) => RemoveFromWindows();
        settingsViewModel.HasOtherProfiles = _settingsStore?.ListOtherProfiles().Count > 0;

        var settingsWindow = new SettingsWindow(_settings.Theme) { DataContext = settingsViewModel };
        _settingsWindow = settingsWindow;

        if (_settings.SettingsWidth is double width && width > 0)
        {
            settingsWindow.Width = width;
        }

        if (_settings.SettingsHeight is double height && height > 0)
        {
            settingsWindow.Height = height;
        }

        settingsWindow.Closing += (_, e) =>
        {
            if (!e.Cancel)
            {
                RememberSettingsSize(settingsWindow.RestoreBounds);
            }
        };
        _settingsWindow.Closed += (_, _) =>
        {
            settingsViewModel.Dispose();
            _settingsViewModel = null;
            _settingsWindow = null;
            EndAppearancePreview();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    // Opens the editor as a dialog; afterwards the widget shows what the settings window still has pending.
    private void EditAppearance(AppearanceGroup group)
    {
        if (_settingsWindow is null || _settingsViewModel is null)
        {
            return;
        }

        SettingsViewModel settingsViewModel = _settingsViewModel;
        AppearancePreview shown = settingsViewModel.ShownAppearance;
        var editor = new AppearanceEditorViewModel(group, shown.Appearance, shown.Colors);
        var window = new AppearanceEditorWindow(_settings.Theme) { DataContext = editor, Owner = _settingsWindow };

        editor.Previewed += (_, preview) => PreviewAppearance(preview);
        editor.Applied += (_, preview) =>
        {
            CommitAppearance(preview);
            window.Close();
        };

        window.ShowDialog();
        ShowPendingAppearance(settingsViewModel);
    }

    // Keeps previewing changes not applied yet, or goes back to the saved look.
    private void ShowPendingAppearance(SettingsViewModel? settingsViewModel)
    {
        if (settingsViewModel?.PendingAppearance is AppearancePreview pending)
        {
            PreviewAppearance(pending);
        }
        else
        {
            EndAppearancePreview();
        }
    }

    private void PreviewAppearance(AppearancePreview preview)
    {
        _viewModel?.PreviewAppearance(preview.Appearance, preview.Colors);

        if (_window is not null)
        {
            _window.ScalePercent = ResizeRule.ClampPercent(preview.Appearance.Scale);
        }
    }

    // "Apply" in the editor saves right away.
    private void CommitAppearance(AppearancePreview preview)
    {
        _settings.Appearance = preview.Appearance;
        _settings.Colors = preview.Colors;
        _settingsViewModel?.SyncAppearance(preview.Appearance, preview.Colors);
        SaveSettings();
    }

    // Back to the saved appearance.
    private void EndAppearancePreview()
    {
        _viewModel?.UpdateSettings(CurrentSettings());

        if (_window is not null)
        {
            _window.ScalePercent = ResizeRule.ClampPercent(_settings.Appearance.Scale);
        }
    }

    // The list reloads whenever Windows reports an address or connectivity change.
    private void OpenNetworkDevicesWindow()
    {
        if (_networkDevicesWindow is not null)
        {
            _networkDevicesWindow.Activate();
            return;
        }

        if (_http is null)
        {
            return;
        }

        HttpClient http = _http;
        var wifiReader = new WifiReader();
        var viewModel = new NetworkDevicesViewModel(
            _settings.Network,
            ListNetworkDevices,
            wifiReader.Read,
            cancellationToken => PublicAddress.FetchAsync(http, cancellationToken));
        var window = new NetworkDevicesWindow(_settings.Theme) { DataContext = viewModel };

        _networkDevicesViewModel = viewModel;
        _networkDevicesWindow = window;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        window.Closed += (_, _) =>
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            viewModel.Dispose();
            wifiReader.Dispose();
            _networkDevicesViewModel = null;
            _networkDevicesWindow = null;
        };
        window.Show();
        window.Activate();
        ReloadNetworkDevices();
    }

    private void OpenAboutWindow()
    {
        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        if (_http is null)
        {
            return;
        }

        var checker = new UpdateChecker(_http);
        var viewModel = new AboutViewModel(checker.LatestAsync, OpenExternal);
        var window = new AboutWindow(_settings.Theme, IconResource) { DataContext = viewModel };

        _aboutWindow = window;
        window.Closed += (_, _) =>
        {
            viewModel.Dispose();
            _aboutWindow = null;
        };
        window.Show();
        window.Activate();
    }

    // Opens a link or folder with its default app.
    private void OpenExternal(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            MessageBox.Show(Localizer.Format(Strings.Format_OpenFailed, target), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(ReloadNetworkDevices);

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => Dispatcher.InvokeAsync(ReloadNetworkDevices);

    private async void ReloadNetworkDevices()
    {
        if (_networkDevicesViewModel is NetworkDevicesViewModel viewModel)
        {
            await viewModel.ReloadAsync();
        }
    }

    private async void UpdateNetworkDevices(NetworkSnapshot traffic)
    {
        if (_networkDevicesViewModel is NetworkDevicesViewModel viewModel)
        {
            await viewModel.UpdateAsync(traffic);
        }
    }

    // A listing error just shows an empty list.
    private static IReadOnlyList<NetworkDeviceInfo> ListNetworkDevices()
    {
        try
        {
            return NetworkDevices.List();
        }
        catch (Exception exception) when (exception is NetworkInformationException or Win32Exception)
        {
            return [];
        }
    }

    // RestoreBounds keeps the normal size even when the window is maximized.
    private void RememberSettingsSize(Rect bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        _settings.SettingsWidth = bounds.Width;
        _settings.SettingsHeight = bounds.Height;
        SaveSettings();
    }

    // Called by "Apply" in the settings window.
    private void ApplySavedSettings(SavedSettings saved)
    {
        bool fpsWasShown = _settings.Display.ShowFps;
        _settings.Display = saved.Display;
        _settings.Colors = saved.Colors;
        _settings.Network = saved.Network;
        _settings.Appearance = saved.Appearance;
        ApplyGeneral(saved.General);
        _viewModel?.UpdateSettings(saved);
        _networkDevicesViewModel?.UpdateSettings(saved.Network);

        if (_window is not null)
        {
            _window.ScalePercent = ResizeRule.ClampPercent(saved.Appearance.Scale);
        }

        RefreshTrayMenu();
        ShowPendingAppearance(_settingsViewModel);

        if (saved.Display.ShowFps != fpsWasShown)
        {
            if (saved.Display.ShowFps)
            {
                EnableFps();
            }
            else
            {
                DisableFps();
            }
        }

        SaveSettings();
    }

    // True when the scheduled task starts this same .exe with this same settings file.
    private bool CanRelaunchElevated() =>
        _settings.Display.ShowFps && !Elevation.IsElevated && _settingsStore is not null && ElevatedTask.PointsTo(ExecutablePath, _settingsStore.FilePath);

    // FPS needs admin rights; without them, permission is requested first.
    private void EnableFps()
    {
        if (Elevation.IsElevated)
        {
            StartFps();
        }
        else
        {
            RequestFpsPermission();
        }
    }

    // Makes sure the task points here, so the next start is already elevated.
    private void StartFps()
    {
        if (_fps is not null || _settingsStore is null)
        {
            return;
        }

        if (!ElevatedTask.PointsTo(ExecutablePath, _settingsStore.FilePath))
        {
            ElevatedTask.Register(ExecutablePath, _settingsStore.FilePath);
        }

        try
        {
            _fps = FpsMonitor.Start();
        }
        catch (Exception exception)
        {
            _trayIcon?.ShowNotification(Strings.Metric_Fps, Localizer.Format(Strings.Format_FpsUnavailable, exception.Message), isError: true);
        }
    }

    // Also removes the task, so the app stops starting elevated.
    private void DisableFps()
    {
        _fps?.Dispose();
        _fps = null;
        _viewModel?.ApplyFps(null);

        if (_settingsStore is not null && ElevatedTask.PointsTo(ExecutablePath, _settingsStore.FilePath))
        {
            ElevatedTask.Delete();
        }
    }

    // Registers the task through a UAC prompt, then restarts elevated.
    private async void RequestFpsPermission()
    {
        if (_fpsPermissionPending || _settingsStore is null)
        {
            return;
        }

        if (!Elevation.CanElevate)
        {
            RejectFps(nameof(Strings.Fps_RequiresAdmin));
            return;
        }

        _fpsPermissionPending = true;
        string? noticeKey;

        try
        {
            noticeKey = await RegisterTaskElevatedAsync(_settingsStore.FilePath);
        }
        finally
        {
            _fpsPermissionPending = false;
        }

        if (noticeKey is not null)
        {
            RejectFps(noticeKey);
            return;
        }

        RelaunchElevated();
    }

    // Runs this .exe as admin only to create the task; returns the notice to show, or null on success.
    private static async Task<string?> RegisterTaskElevatedAsync(string settingsPath)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath, ElevatedTask.RegisterArguments(settingsPath))
        {
            UseShellExecute = true,
            Verb = "runas",
        };

        try
        {
            using Process? process = Process.Start(startInfo);

            if (process is null)
            {
                return nameof(Strings.Fps_Failed);
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? null : nameof(Strings.Fps_Failed);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
        {
            return nameof(Strings.Fps_Denied);
        }
    }

    // If the settings window stays open (pending changes), the restart is postponed and the user is told.
    private void RelaunchElevated()
    {
        _relaunchElevated = true;
        RequestExit();

        if (_settingsWindow is not null)
        {
            _relaunchElevated = false;
            _settingsViewModel?.ShowFpsNotice(nameof(Strings.Fps_RestartPending));
        }
    }

    // Turns FPS off and explains why.
    private void RejectFps(string noticeKey)
    {
        _settings.Display = _settings.Display with { ShowFps = false };
        _viewModel?.UpdateSettings(CurrentSettings());
        ShowPendingAppearance(_settingsViewModel);

        if (_settingsViewModel is not null)
        {
            _settingsViewModel.RejectFps(noticeKey);
        }
        else
        {
            _trayIcon?.ShowNotification(Strings.Metric_Fps, Localizer.Instance[noticeKey], isError: true);
        }

        SaveSettings();
    }

    // Applies the General tab.
    private void ApplyGeneral(GeneralSettings general)
    {
        _settings.Language = general.Language;
        Localizer.Instance.SetLanguage(general.Language);
        _settings.Theme = general.Theme;
        WindowTheme.ApplyAll(general.Theme);
        ApplyTrayTheme();

        if (general.Mode != _settings.Mode)
        {
            SetMode(general.Mode);
        }

        SetSnapToEdges(general.SnapToEdges);
        SetPositionLock(general.PositionLocked);
        SetClickThrough(general.ClickThrough, general.InteractionKey);
        SetStartWithWindows(general.StartWithWindows);

        if (general.Monitor is string monitor)
        {
            _window?.MoveToMonitor(monitor);
        }
    }

    private SavedSettings CurrentSettings() => new(_settings.Display, _settings.Colors, _settings.Network, _settings.Appearance, CurrentGeneral());

    // "Start with Windows" is read from the registry, not from the settings file.
    private GeneralSettings CurrentGeneral() => new()
    {
        Language = _settings.Language,
        Theme = _settings.Theme,
        Mode = _settings.Mode,
        SnapToEdges = _settings.SnapToEdges,
        PositionLocked = _settings.PositionLocked,
        ClickThrough = _settings.ClickThrough,
        InteractionKey = _settings.InteractionKey,
        StartWithWindows = StartupRegistration.IsEnabled(),
        Monitor = _window?.CurrentMonitor,
    };

    private static List<DisplayMonitor> ListMonitors() =>
        DisplayMonitor.ByPosition(Forms.Screen.AllScreens.Select(screen => new ScreenInfo(
            screen.DeviceName,
            screen.Bounds.Left,
            screen.Bounds.Top,
            screen.Bounds.Width,
            screen.Bounds.Height,
            screen.Primary)));

    // From the tray: starts a test, or shows the one already running (its window may have been closed).
    private void StartSpeedTest()
    {
        if (_speedTest is null)
        {
            return;
        }

        if (_speedTest.RunCommand.IsRunning)
        {
            OpenSpeedTestWindow();
        }
        else if (_speedTest.RunCommand.CanExecute(null))
        {
            _speedTest.RunCommand.Execute(null);
        }
    }

    // Keeps the tray tooltip in sync.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.TrayText) && _viewModel is not null)
        {
            _trayIcon?.SetTooltip(_viewModel.TrayText);
        }
    }

    // Updates the tray menu item while a test runs, and opens the speed test window when one starts,
    // wherever it was started (widget, tray or settings).
    private void OnSpeedTestCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning) && _speedTest is not null)
        {
            _trayIcon?.SetSpeedTestRunning(_speedTest.RunCommand.IsRunning);
            if (_speedTest.RunCommand.IsRunning)
            {
                OpenSpeedTestWindow();
            }
        }
    }

    // Closing the window only hides the test, which keeps running; "Cancel test" is what stops it.
    private void OpenSpeedTestWindow()
    {
        if (_speedTestWindow is not null)
        {
            _speedTestWindow.Activate();
            return;
        }

        var window = new SpeedTestWindow(_settings.Theme) { DataContext = _speedTest };
        _speedTestWindow = window;
        window.Closed += (_, _) => _speedTestWindow = null;
        window.Show();
        window.Activate();
    }

    // The result is saved; a notification shows only when the speed test window was closed and can't show it.
    private void OnSpeedTestCompleted(SpeedTestResult result)
    {
        _settings.LastSpeedTest = result;
        SaveSettings();

        if (_speedTestWindow is null && _speedTest is not null)
        {
            _trayIcon?.ShowNotification(Strings.SpeedTest_Title, $"{_speedTest.ResultText}\n{_speedTest.LatencyText}", isError: false);
        }
    }

    private void OnSpeedTestFailed(string message)
    {
        if (_speedTestWindow is null)
        {
            _trayIcon?.ShowNotification(Strings.SpeedTest_Title, message, isError: true);
        }
    }

    private void ResetNetworkSession()
    {
        _monitor?.ResetNetworkSession(_settings.Network.DefaultDevice?.Id);
        SaveSettings();
    }

    private void ApplyTrayTheme() =>
        _trayIcon?.ApplyTheme(WindowsTheme.IsDark(_settings.Theme, WindowsTheme.AppsUseDarkTheme()));

    // Follows the Windows light/dark switch when the theme is "System".
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _settings.Theme == AppTheme.System)
        {
            Dispatcher.InvokeAsync(() =>
            {
                WindowTheme.ApplyAll(_settings.Theme);
                ApplyTrayTheme();
            });
        }
    }

    private void RefreshTrayMenu() =>
        _trayIcon?.UpdateMenu(_settings);

    private void SaveSettings() => SaveSettings(TimeSpan.Zero);

    // Saves the live window position and network totals together with the settings.
    private void SaveSettings(TimeSpan patience)
    {
        if (_settingsStore is null || _window is null || _monitor is null)
        {
            return;
        }

        CaptureLiveState(_window, _monitor);
        _settingsStore.Save(_settings, patience);
    }

    // The position is saved in pixels, so the old DIP fields are cleared.
    private void CaptureLiveState(MainWindow window, HardwareMonitor monitor)
    {
        if (window.PixelPosition is Interop.User32.Point position)
        {
            _settings.WindowX = position.X;
            _settings.WindowY = position.Y;
            _settings.WindowLeft = null;
            _settings.WindowTop = null;
        }

        _settings.NetworkSession = monitor.ExportNetworkSession();
    }

    // Lists the other profiles and deletes them after confirmation.
    private void RemoveOtherProfiles()
    {
        if (_settingsStore is null || _window is null || _monitor is null || _settingsViewModel is null)
        {
            return;
        }

        IReadOnlyList<StoredProfile> others = _settingsStore.ListOtherProfiles();

        if (others.Count > 0)
        {
            string list = string.Join(Environment.NewLine, others.Select(profile =>
                Localizer.Format(Strings.Format_StoredProfile, profile.Computer, profile.UserName, profile.LastUsed?.LocalDateTime.ToString("d", Localizer.Instance.Culture) ?? DisplayFormat.Unavailable)));
            string question = string.Join(Environment.NewLine + Environment.NewLine, Strings.RemoveProfiles_Question, list, Strings.RemoveProfiles_Keep);

            if (MessageBox.Show(_settingsWindow!, question, Strings.Settings_Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            CaptureLiveState(_window, _monitor);

            if (!_settingsStore.RemoveOtherProfiles(_settings))
            {
                MessageBox.Show(_settingsWindow!, Strings.RemoveProfiles_Failed, Strings.Settings_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        _settingsViewModel.HasOtherProfiles = _settingsStore.ListOtherProfiles().Count > 0;
    }

    // Removes what the app registered in Windows: the startup entry and the FPS task.
    private void RemoveFromWindows()
    {
        if (_settingsWindow is null || _settingsViewModel is null)
        {
            return;
        }

        bool startup = StartupRegistration.IsEnabled();
        bool task = ElevatedTask.Exists();

        if (!startup && !task)
        {
            MessageBox.Show(_settingsWindow, Strings.RemoveFromWindows_Nothing, Strings.Settings_Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IEnumerable<string> items = new[] { (startup, Strings.RemoveFromWindows_Startup), (task, Strings.RemoveFromWindows_FpsTask) }
            .Where(item => item.Item1)
            .Select(item => Localizer.Format(Strings.Format_ListItem, item.Item2));
        string question = string.Join(Environment.NewLine + Environment.NewLine, Strings.RemoveFromWindows_Question, string.Join(Environment.NewLine, items), Strings.RemoveFromWindows_Note);

        if (MessageBox.Show(_settingsWindow, question, Strings.Settings_Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        SetStartWithWindows(false);
        _settingsViewModel.SyncGeneral(CurrentGeneral());

        if (_settings.Display.ShowFps)
        {
            _settings.Display = _settings.Display with { ShowFps = false };
            DisableFps();
            _viewModel?.UpdateSettings(CurrentSettings());
            _settingsViewModel.SyncFpsOff();
        }

        bool removed = !ElevatedTask.Exists() || ElevatedTask.Delete();
        SaveSettings();
        MessageBox.Show(
            _settingsWindow,
            removed ? Strings.RemoveFromWindows_Done : Strings.RemoveFromWindows_Failed,
            Strings.Settings_Title,
            MessageBoxButton.OK,
            removed ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    // Samples once per second on a background thread, then updates the UI on the UI thread.
    private async void RunSamplingLoop(HardwareMonitor monitor, MainViewModel viewModel, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                HardwareSnapshot snapshot = await Task.Run(() => monitor.Sample(), cancellationToken);
                viewModel.Apply(snapshot);
                UpdateNetworkDevices(snapshot.Network);
                viewModel.ApplyFps(_fps?.Sample());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    // Pings right away and then every 5 seconds; a result is dropped if the host changed meanwhile.
    private async void RunPingLoop(MainViewModel viewModel, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PingInterval);

        try
        {
            do
            {
                if (!_settings.Network.PingEnabled)
                {
                    continue;
                }

                string host = _settings.Network.PingHost;
                long? roundTrip = await LatencyProbe.PingAsync(host, PingTimeout, cancellationToken);

                if (_settings.Network.PingEnabled && host == _settings.Network.PingHost)
                {
                    viewModel.ApplyPing(roundTrip);
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    // Any unhandled UI error shows the error window and closes the app.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        if (!_showingError)
        {
            _showingError = true;
            ShowError(e.Exception);
        }

        Shutdown(1);
    }

    // Falls back to a message box if the error window itself fails.
    private void ShowError(Exception exception)
    {
        try
        {
            new ErrorWindow(exception, _settings.Theme).ShowDialog();
        }
        catch (Exception windowException)
        {
            MessageBox.Show(
                $"{AppInfo.DiagnosticHeader}{Environment.NewLine}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}{windowException}",
                Strings.App_UnexpectedError,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
