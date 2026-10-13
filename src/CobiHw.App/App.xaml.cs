using System.Windows;
using System.Windows.Threading;
using CobiHw.App.Settings;
using CobiHw.App.Tray;
using CobiHw.Core;

namespace CobiHw.App;

public partial class App : Application
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(1);
    private static readonly Uri IconResource = new("pack://application:,,,/Assets/app.ico");

    private readonly CancellationTokenSource _shutdown = new();
    private SingleInstance? _singleInstance;
    private SettingsStore? _settingsStore;
    private AppSettings _settings = new();
    private HardwareMonitor? _monitor;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private TrayIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
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
        StartupRegistration.RepairIfMoved();

        _monitor = new HardwareMonitor(_settings.NetworkSession);
        _viewModel = new MainViewModel(_settings.Display, _settings.Colors) { Layout = _settings.Layout };

        _window = new MainWindow
        {
            DataContext = _viewModel,
            IsPositionLocked = _settings.PositionLocked,
            SnapToEdges = _settings.SnapToEdges,
        };
        _window.RestorePosition(_settings.WindowLeft, _settings.WindowTop);
        _window.ApplyMode(_settings.Mode);
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
        _trayIcon.StartWithWindowsToggled += (_, enabled) => ChangeStartWithWindows(enabled);
        _trayIcon.SettingsRequested += (_, _) => OpenSettingsWindow();
        _trayIcon.ResetNetworkRequested += (_, _) => ResetNetworkSession();
        _trayIcon.ExitRequested += (_, _) => Shutdown();
        RefreshTrayMenu();

        _singleInstance.ListenForShowRequests(() => Dispatcher.InvokeAsync(ShowWindow));

        _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveInterval };
        _autoSaveTimer.Tick += (_, _) => SaveSettings();
        _autoSaveTimer.Start();

        RunSamplingLoop(_monitor, _viewModel, _shutdown.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _autoSaveTimer?.Stop();
        SaveSettings();
        _trayIcon?.Dispose();
        _monitor?.Dispose();
        _singleInstance?.Dispose();
        _shutdown.Dispose();
        base.OnExit(e);
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

        var settingsViewModel = new SettingsViewModel(_settings.Display, _settings.Colors, _viewModel?.AvailableDisks ?? []);
        settingsViewModel.Applied += (_, saved) => ApplySavedSettings(saved);

        _settingsWindow = new SettingsWindow { DataContext = settingsViewModel };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ApplySavedSettings(SavedSettings saved)
    {
        _settings.Display = saved.Display;
        _settings.Colors = saved.Colors;
        _viewModel?.UpdateSettings(saved.Display, saved.Colors);
        SaveSettings();
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

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.ToString(),
            "cobihw: erro inesperado",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(1);
    }
}
