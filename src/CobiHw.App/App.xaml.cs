using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using CobiHw.App.Tray;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.App;

public partial class App : Application
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HttpRequestTimeout = TimeSpan.FromSeconds(60);
    private static readonly Uri IconResource = new("pack://application:,,,/Assets/app.ico");

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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _singleInstance = SingleInstance.TryAcquire();
        if (_singleInstance is null)
        {
            Shutdown();
            return;
        }

        _settingsStore = SettingsStore.CreateBesideExecutable();
        _settings = _settingsStore.Load();
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
        };
        _window.RestorePosition(_settings.WindowLeft, _settings.WindowTop);
        _window.ApplyMode(_settings.Mode);
        _window.ApplyClickThrough(_settings.Display.ClickThrough, _settings.Display.InteractionKey);
        _window.DragCompleted += (_, _) => SaveSettings();
        _window.LayoutCycleRequested += (_, _) => ChangeLayout(NextLayout(_settings.Layout));
        MainWindow = _window;
        _window.Show();

        _trayIcon = new TrayIcon(IconResource);
        _trayIcon.ToggleVisibilityRequested += (_, _) => ToggleWindowVisibility();
        _trayIcon.ModeSelected += (_, mode) => ChangeMode(mode);
        _trayIcon.LayoutSelected += (_, layout) => ChangeLayout(layout);
        _trayIcon.SnapToEdgesToggled += (_, enabled) => ChangeSnapToEdges(enabled);
        _trayIcon.LockPositionToggled += (_, locked) => ChangePositionLock(locked);
        _trayIcon.ClickThroughToggled += (_, enabled) => ChangeClickThrough(enabled);
        _trayIcon.StartWithWindowsToggled += (_, enabled) => ChangeStartWithWindows(enabled);
        _trayIcon.SettingsRequested += (_, _) => OpenSettingsWindow();
        _trayIcon.SpeedTestRequested += (_, _) => StartSpeedTest();
        _trayIcon.ResetNetworkRequested += (_, _) => ResetNetworkSession();
        _trayIcon.ExitRequested += (_, _) => Shutdown();
        RefreshTrayMenu();

        _singleInstance.ListenForShowRequests(() => Dispatcher.InvokeAsync(ShowWindow));

        _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveInterval };
        _autoSaveTimer.Tick += (_, _) => SaveSettings();
        _autoSaveTimer.Start();

        RunSamplingLoop(_monitor, _viewModel, _shutdown.Token);
        RunPingLoop(_viewModel, _shutdown.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _speedTest?.RunCancelCommand.Execute(null);
        _shutdown.Cancel();
        _autoSaveTimer?.Stop();
        SaveSettings();
        _trayIcon?.Dispose();
        _monitor?.Dispose();
        _http?.Dispose();
        _singleInstance?.Dispose();
        _shutdown.Dispose();
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

    private void ChangeMode(WidgetMode mode)
    {
        _settings.Mode = mode;
        _window?.ApplyMode(mode);
        RefreshTrayMenu();
        SaveSettings();
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
        _settings.SnapToEdges = enabled;

        if (_window is not null)
        {
            _window.SnapToEdges = enabled;
        }

        RefreshTrayMenu();
        SaveSettings();
    }

    private void ChangePositionLock(bool locked)
    {
        _settings.PositionLocked = locked;

        if (_window is not null)
        {
            _window.IsPositionLocked = locked;
        }

        RefreshTrayMenu();
        SaveSettings();
    }

    private void ChangeClickThrough(bool enabled)
    {
        _settings.Display = _settings.Display with { ClickThrough = enabled };
        _window?.ApplyClickThrough(enabled, _settings.Display.InteractionKey);
        _settingsViewModel?.SyncClickThrough(enabled);
        RefreshTrayMenu();
        SaveSettings();
    }

    private void ChangeStartWithWindows(bool enabled)
    {
        if (enabled)
        {
            StartupRegistration.Enable();
        }
        else
        {
            StartupRegistration.Disable();
        }

        RefreshTrayMenu();
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

        var settingsViewModel = new SettingsViewModel(CurrentSettings(), _viewModel.AvailableDisks, _speedTest);
        _settingsViewModel = settingsViewModel;
        settingsViewModel.Applied += (_, saved) => ApplySavedSettings(saved);

        _settingsWindow = new SettingsWindow { DataContext = settingsViewModel };
        _settingsWindow.Closed += (_, _) =>
        {
            settingsViewModel.Dispose();
            _settingsViewModel = null;
            _settingsWindow = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ApplySavedSettings(SavedSettings saved)
    {
        _settings.Display = saved.Display;
        _settings.Colors = saved.Colors;
        _settings.Network = saved.Network;
        _settings.Appearance = saved.Appearance;
        _settings.Language = saved.Language;
        Localizer.Instance.SetLanguage(saved.Language);
        _viewModel?.UpdateSettings(saved);
        _window?.ApplyClickThrough(saved.Display.ClickThrough, saved.Display.InteractionKey);
        RefreshTrayMenu();
        SaveSettings();
    }

    private SavedSettings CurrentSettings() => new(_settings.Display, _settings.Colors, _settings.Network, _settings.Appearance, _settings.Language);

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

    private void RefreshTrayMenu() =>
        _trayIcon?.UpdateMenu(_settings, StartupRegistration.IsEnabled());

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

    private static async void RunSamplingLoop(HardwareMonitor monitor, MainViewModel viewModel, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                HardwareSnapshot snapshot = await Task.Run(() => monitor.Sample(), cancellationToken);
                viewModel.Apply(snapshot);
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
