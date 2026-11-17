using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using CobiHw.App.Linux.Tray;
using CobiHw.Core;
using CobiHw.Core.Linux;
using CobiHw.Core.Network;
using CobiHw.Presentation;
using CobiHw.Presentation.Localization;
using CobiHw.Presentation.Resources;
using CobiHw.Presentation.Settings;

namespace CobiHw.App.Linux;

/// <summary>
/// Starts the Linux app: loads the settings, creates the widget and the tray icon, and runs the sampling and ping loops.
/// </summary>
/// <remarks>
/// Mirrors the Windows App: Change* methods come from the tray menu and save; Set* methods only apply a value.
/// What Linux doesn't have yet (settings window, About, autostart, FPS) is left out rather than stubbed.
/// </remarks>
public partial class App : Application
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(1);
    // How long to wait for a settings file another process is writing.
    private static readonly TimeSpan SettingsLoadPatience = TimeSpan.FromSeconds(5);
    // Exiting must not hang on a busy file; the autosave has kept the file recent anyway.
    private static readonly TimeSpan ExitSavePatience = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(2);
    // The speed test downloads for several seconds; the default 100 s would hide a stalled one.
    private static readonly TimeSpan HttpRequestTimeout = TimeSpan.FromSeconds(60);

    private readonly CancellationTokenSource _shutdown = new();
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private SettingsStore? _settingsStore;
    private AppSettings _settings = new();
    private HardwareMonitor? _monitor;
    private HttpClient? _http;
    private SpeedTestViewModel? _speedTest;
    private MainViewModel? _viewModel;
    private WidgetWindow? _window;
    private TrayMenu? _tray;
    private DispatcherTimer? _autoSaveTimer;
    private bool _exiting;

    /// <summary>The single-instance lock taken in <see cref="Program.Main"/>; it hears the show requests of later launches.</summary>
    internal static SingleInstance? Instance { get; set; }

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            // A shutdown the session asks for (logout) closes the widget too; its Closed must not start a second one.
            desktop.ShutdownRequested += (_, _) => _exiting = true;
            desktop.Exit += (_, _) => OnExit();
            Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Start()
    {
        _settingsStore = SettingsStore.InFolder(LinuxProfile.SettingsFolder, LinuxProfile.Current());
        _settings = _settingsStore.Load(SettingsLoadPatience);

        Localizer.Instance.SetLanguage(_settings.Language);
        Localizer.Instance.CultureChanged += (_, _) => _tray?.ApplyTexts();
        RequestedThemeVariant = ThemeFor(_settings.Theme);

        _monitor = new HardwareMonitor(_settings.NetworkSession);
        _http = CreateHttpClient();
        _speedTest = new SpeedTestViewModel(new CloudflareSpeedTest(_http), _settings.LastSpeedTest);
        _speedTest.Completed += (_, result) => OnSpeedTestCompleted(result);
        _viewModel = new MainViewModel(CurrentSettings(), _speedTest) { Layout = _settings.Layout };

        _window = new WidgetWindow
        {
            DataContext = _viewModel,
            IsPositionLocked = _settings.PositionLocked,
            SnapToEdges = _settings.SnapToEdges,
            ScalePercent = ResizeRule.ClampPercent(_settings.Appearance.Scale),
        };
        _window.RestorePosition(_settings.WindowX, _settings.WindowY);
        _window.ApplyMode(_settings.Mode);
        _window.ApplyClickThrough(_settings.ClickThrough);
        _window.DragCompleted += (_, _) => SaveSettings();
        _window.LayoutCycleRequested += (_, _) => ChangeLayout(NextLayout(_settings.Layout));
        _window.ScaleDragged += (_, percent) => _viewModel?.ApplyScale(percent);
        _window.ScaleCompleted += (_, percent) => ChangeScale(percent);
        // The window manager can close any window (Alt+F4, a panel's close action); the widget is the app, so that's an exit.
        _window.Closed += (_, _) => RequestExit();
        _window.Show();

        _tray = new TrayMenu(this);
        _tray.ShowWidgetToggled += (_, _) => ToggleWindowVisibility();
        _tray.ModeSelected += (_, mode) => ChangeMode(mode);
        _tray.LayoutSelected += (_, layout) => ChangeLayout(layout);
        _tray.SnapToEdgesToggled += (_, _) => ChangeSnapToEdges(!_settings.SnapToEdges);
        _tray.LockPositionToggled += (_, _) => ChangePositionLock(!_settings.PositionLocked);
        _tray.ClickThroughToggled += (_, _) => ChangeClickThrough(!_settings.ClickThrough);
        _tray.ResetNetworkRequested += (_, _) => ResetNetworkSession();
        _tray.ExitRequested += (_, _) => RequestExit();
        RefreshTrayMenu();
        _tray.SetTooltip(_viewModel.TrayText);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Instance?.ListenForShowRequests(() => Dispatcher.UIThread.Post(ShowWindow));

        _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveInterval };
        _autoSaveTimer.Tick += (_, _) => SaveSettings();
        _autoSaveTimer.Start();

        RunSamplingLoop(_monitor, _viewModel, _shutdown.Token);
        RunPingLoop(_viewModel, _shutdown.Token);

        // No message window on Linux until the settings window exists (PR 4b); a terminal launch still shows why nothing is saved.
        if (_settingsStore.Access == SettingsAccess.FolderNotWritable)
        {
            Console.Error.WriteLine($"{LinuxProfile.SettingsFolder}: {Strings.Settings_FolderReadOnly}");
        }
    }

    // Stops the loops, saves the settings and releases resources.
    private void OnExit()
    {
        _exiting = true;
        _speedTest?.RunCancelCommand.Execute(null);
        _shutdown.Cancel();
        _autoSaveTimer?.Stop();
        SaveSettings(ExitSavePatience);
        _tray?.Dispose();
        _monitor?.Dispose();
        _http?.Dispose();
        _shutdown.Dispose();
    }

    private void RequestExit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _desktop?.Shutdown();
    }

    // One shared client for the speed test, the update check and the public IP.
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = HttpRequestTimeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("cobihw");
        return http;
    }

    private static ThemeVariant ThemeFor(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

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

        RefreshTrayMenu();
    }

    private void ShowWindow()
    {
        if (_window is null || _window.IsVisible)
        {
            return;
        }

        _window.Show();
        _window.ReapplyState();
        RefreshTrayMenu();
    }

    // After a change from the tray menu: refreshes the menu, then saves.
    private void AfterGeneralChange()
    {
        RefreshTrayMenu();
        SaveSettings();
    }

    private void ChangeMode(WidgetMode mode)
    {
        _settings.Mode = mode;
        _window?.ApplyMode(mode);
        AfterGeneralChange();
    }

    private void ChangeLayout(ViewLayout layout)
    {
        _settings.Layout = layout;

        if (_viewModel is not null)
        {
            _viewModel.Layout = layout;
        }

        AfterGeneralChange();
    }

    private void ChangeSnapToEdges(bool enabled)
    {
        _settings.SnapToEdges = enabled;

        if (_window is not null)
        {
            _window.SnapToEdges = enabled;
        }

        AfterGeneralChange();
    }

    private void ChangePositionLock(bool locked)
    {
        _settings.PositionLocked = locked;

        if (_window is not null)
        {
            _window.IsPositionLocked = locked;
        }

        AfterGeneralChange();
    }

    private void ChangeClickThrough(bool enabled)
    {
        _settings.ClickThrough = enabled;
        _window?.ApplyClickThrough(enabled);
        AfterGeneralChange();
    }

    private void ChangeScale(int percent)
    {
        _settings.Appearance = _settings.Appearance with { Scale = percent };
        SaveSettings();
    }

    private SavedSettings CurrentSettings() => new(_settings.Display, _settings.Colors, _settings.Network, _settings.Appearance, CurrentGeneral());

    // Autostart (PR 4c) and the monitor choice (PR 4b) aren't on Linux yet.
    private GeneralSettings CurrentGeneral() => new()
    {
        Language = _settings.Language,
        Theme = _settings.Theme,
        Mode = _settings.Mode,
        SnapToEdges = _settings.SnapToEdges,
        PositionLocked = _settings.PositionLocked,
        ClickThrough = _settings.ClickThrough,
        InteractionKey = _settings.InteractionKey,
    };

    // Keeps the tray tooltip in sync.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.TrayText) && _viewModel is not null)
        {
            _tray?.SetTooltip(_viewModel.TrayText);
        }
    }

    private void OnSpeedTestCompleted(SpeedTestResult result)
    {
        _settings.LastSpeedTest = result;
        SaveSettings();
    }

    private void ResetNetworkSession()
    {
        _monitor?.ResetNetworkSession(_settings.Network.DefaultDevice?.Id);
        SaveSettings();
    }

    private void RefreshTrayMenu() =>
        _tray?.Update(_settings, _window?.IsVisible == true);

    private void SaveSettings() => SaveSettings(TimeSpan.Zero);

    // Saves the live window position and network totals together with the settings.
    private void SaveSettings(TimeSpan patience)
    {
        if (_settingsStore is null || _window is null || _monitor is null)
        {
            return;
        }

        CaptureLiveState(_window, _monitor);
        _ = _settingsStore.Save(_settings, patience);
    }

    // The position is saved in pixels, so the old DIP fields of the Windows app are cleared.
    private void CaptureLiveState(WidgetWindow window, HardwareMonitor monitor)
    {
        PixelPoint position = window.PixelPosition;
        _settings.WindowX = position.X;
        _settings.WindowY = position.Y;
        _settings.WindowLeft = null;
        _settings.WindowTop = null;
        _settings.NetworkSession = monitor.ExportNetworkSession();
    }

    // Samples once per second on a background thread, then updates the UI on the UI thread.
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
}
