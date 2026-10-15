using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using CobiHw.App.Tray;
using CobiHw.Core;
using CobiHw.Core.Fps;
using CobiHw.Core.Network;
using Forms = System.Windows.Forms;

namespace CobiHw.App;

public partial class App : Application
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HttpRequestTimeout = TimeSpan.FromSeconds(60);
    private static readonly Uri IconResource = new("pack://application:,,,/Assets/app.ico");
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
    private DispatcherTimer? _autoSaveTimer;
    private FpsMonitor? _fps;
    private bool _fpsPermissionPending;
    private bool _relaunchElevated;

    private static string ExecutablePath => Environment.ProcessPath
        ?? throw new InvalidOperationException("O caminho do executável não está disponível.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        string? settingsPath = ElevatedTask.SettingsPathFrom(e.Args);

        if (e.Args.Contains(ElevatedTask.RegisterArgument))
        {
            Shutdown(settingsPath is not null && ElevatedTask.Register(ExecutablePath, settingsPath) ? 0 : 1);
            return;
        }

        _settingsStore = settingsPath is null ? SettingsStore.CreateBesideExecutable() : SettingsStore.At(settingsPath);
        _settings = _settingsStore.Load();

        if (settingsPath is null && CanRelaunchElevated() && ElevatedTask.Run())
        {
            Shutdown();
            return;
        }

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
        _window.RestorePosition(_settings.WindowLeft, _settings.WindowTop);
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
        _trayIcon.SpeedTestRequested += (_, _) => StartSpeedTest();
        _trayIcon.ResetNetworkRequested += (_, _) => ResetNetworkSession();
        _trayIcon.ExitRequested += (_, _) => RequestExit();
        RefreshTrayMenu();
        ApplyTrayTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        _singleInstance.ListenForShowRequests(() => Dispatcher.InvokeAsync(ShowWindow));

        _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveInterval };
        _autoSaveTimer.Tick += (_, _) => SaveSettings();
        _autoSaveTimer.Start();

        if (_settings.Display.ShowFps)
        {
            EnableFps();
        }

        RunSamplingLoop(_monitor, _viewModel, _shutdown.Token);
        RunPingLoop(_viewModel, _shutdown.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _speedTest?.RunCancelCommand.Execute(null);
        _shutdown.Cancel();
        _autoSaveTimer?.Stop();
        SaveSettings();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _trayIcon?.Dispose();
        _fps?.Dispose();
        _monitor?.Dispose();
        _http?.Dispose();
        _singleInstance?.Dispose();
        _shutdown.Dispose();

        if (_relaunchElevated)
        {
            ElevatedTask.Run();
        }

        base.OnExit(e);
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = HttpRequestTimeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("cobihw");
        return http;
    }

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

    private void RequestExit()
    {
        _settingsWindow?.Close();

        if (_settingsWindow is null)
        {
            Shutdown();
        }
    }

    private void AfterWindowMoved()
    {
        SaveSettings();
        _settingsViewModel?.SyncGeneral(CurrentGeneral());
    }

    private void AfterGeneralChange()
    {
        RefreshTrayMenu();
        _settingsViewModel?.SyncGeneral(CurrentGeneral());
        SaveSettings();
    }

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

    private void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        if (_viewModel is null || _speedTest is null)
        {
            return;
        }

        var settingsViewModel = new SettingsViewModel(CurrentSettings(), _viewModel.AvailableDisks, ListMonitors(), _speedTest);
        _settingsViewModel = settingsViewModel;
        settingsViewModel.Applied += (_, saved) => ApplySavedSettings(saved);

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
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

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

    private void ApplySavedSettings(SavedSettings saved)
    {
        bool fpsWasShown = _settings.Display.ShowFps;
        _settings.Display = saved.Display;
        _settings.Colors = saved.Colors;
        _settings.Network = saved.Network;
        _settings.Appearance = saved.Appearance;
        ApplyGeneral(saved.General);
        _viewModel?.UpdateSettings(saved);

        if (_window is not null)
        {
            _window.ScalePercent = ResizeRule.ClampPercent(saved.Appearance.Scale);
        }

        RefreshTrayMenu();

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

    private bool CanRelaunchElevated() =>
        _settings.Display.ShowFps && !Elevation.IsElevated && _settings.FpsTaskPath == ExecutablePath;

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

    private void StartFps()
    {
        if (_fps is not null || _settingsStore is null)
        {
            return;
        }

        if (_settings.FpsTaskPath != ExecutablePath && ElevatedTask.Register(ExecutablePath, _settingsStore.FilePath))
        {
            _settings.FpsTaskPath = ExecutablePath;
            SaveSettings();
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

    private void DisableFps()
    {
        _fps?.Dispose();
        _fps = null;
        _viewModel?.ApplyFps(null);

        if (_settings.FpsTaskPath is not null)
        {
            ElevatedTask.Delete();
            _settings.FpsTaskPath = null;
        }
    }

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

        _settings.FpsTaskPath = ExecutablePath;
        RelaunchElevated();
    }

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

    private void RejectFps(string noticeKey)
    {
        _settings.Display = _settings.Display with { ShowFps = false };
        _viewModel?.UpdateSettings(CurrentSettings());

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

    private void ApplyGeneral(GeneralSettings general)
    {
        _settings.Language = general.Language;
        Localizer.Instance.SetLanguage(general.Language);
        _settings.Theme = general.Theme;
        _settingsWindow?.ApplyTheme(general.Theme);
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

    private void StartSpeedTest()
    {
        if (_speedTest is not null && _speedTest.RunCommand.CanExecute(null))
        {
            _speedTest.RunCommand.Execute(null);
        }
    }

    private void OnSpeedTestCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning) && _speedTest is not null)
        {
            _trayIcon?.SetSpeedTestRunning(_speedTest.RunCommand.IsRunning);
        }
    }

    private void OnSpeedTestCompleted(SpeedTestResult result)
    {
        _settings.LastSpeedTest = result;
        SaveSettings();

        if (_settingsWindow is null && _speedTest is not null)
        {
            _trayIcon?.ShowNotification(Strings.SpeedTest_Title, $"{_speedTest.ResultText}\n{_speedTest.LatencyText}", isError: false);
        }
    }

    private void OnSpeedTestFailed(string message)
    {
        if (_settingsWindow is null)
        {
            _trayIcon?.ShowNotification(Strings.SpeedTest_Title, message, isError: true);
        }
    }

    private void ResetNetworkSession()
    {
        _monitor?.ResetNetworkSession();
        SaveSettings();
    }

    private void ApplyTrayTheme() =>
        _trayIcon?.ApplyTheme(WindowsTheme.IsDark(_settings.Theme, WindowsTheme.AppsUseDarkTheme()));

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _settings.Theme == AppTheme.System)
        {
            Dispatcher.InvokeAsync(ApplyTrayTheme);
        }
    }

    private void RefreshTrayMenu() =>
        _trayIcon?.UpdateMenu(_settings);

    private void SaveSettings()
    {
        if (_settingsStore is null || _window is null || _monitor is null)
        {
            return;
        }

        _settings.WindowLeft = _window.Left;
        _settings.WindowTop = _window.Top;
        _settings.NetworkSession = _monitor.ExportNetworkSession();
        _settingsStore.Save(_settings);
    }

    private async void RunSamplingLoop(HardwareMonitor monitor, MainViewModel viewModel, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                HardwareSnapshot snapshot = await Task.Run(() => monitor.Sample(), cancellationToken);
                viewModel.Apply(snapshot);
                viewModel.ApplyFps(_fps?.Sample());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async void RunPingLoop(MainViewModel viewModel, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PingInterval);

        try
        {
            do
            {
                string host = _settings.Network.PingHost;
                long? roundTrip = await LatencyProbe.PingAsync(host, PingTimeout, cancellationToken);

                if (host == _settings.Network.PingHost)
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

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.ToString(),
            Strings.App_UnexpectedError,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(1);
    }
}
