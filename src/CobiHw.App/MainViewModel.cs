using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using CobiHw.App.Tray;
using CobiHw.Core;
using CobiHw.Core.Fps;

namespace CobiHw.App;

/// <summary>
/// State of the widget: every text, color and visibility the main window binds to.
/// Readings arrive once per second through <see cref="Apply"/>; settings changes through <see cref="UpdateSettings"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    // Bar color while usage colors are off.
    private const string AccentColor = "#4FB0EA";
    // Labels and dividers use the text color with less opacity.
    private const byte LabelAlpha = 0xA6;
    private const byte DividerAlpha = 0x33;

    private DisplaySettings _display;
    private ColorSettings _colors;
    private NetworkDeviceRef? _networkDevice;
    // Latest readings, kept so settings or language changes redraw without waiting for the next sample.
    private HardwareSnapshot? _lastSnapshot;
    private FpsSnapshot? _lastFps;
    // Whether the FPS row is in the item lists.
    private bool _fpsShown;
    private long? _lastPing;
    private bool _hasBattery;
    // Raw network speeds for the tray, which shows them without units.
    private double? _downloadBytesPerSecond;
    private double? _uploadBytesPerSecond;

    /// <summary>Builds the state from the saved settings.</summary>
    /// <param name="settings">Settings to show.</param>
    /// <param name="speedTest">Speed test shared with the settings window.</param>
    public MainViewModel(SavedSettings settings, SpeedTestViewModel speedTest)
    {
        _display = settings.Display;
        _colors = settings.Colors;
        SpeedTest = speedTest;
        PingHost = settings.Network.PingHost;
        PingEnabled = settings.Network.PingEnabled;
        _networkDevice = settings.Network.DefaultDevice;
        ApplyDisplay();
        ApplyAppearance(settings.Appearance);
        Localizer.Instance.CultureChanged += (_, _) => RefreshTexts();
    }

    /// <summary>Speed test whose progress replaces the network speeds while it runs.</summary>
    public SpeedTestViewModel SpeedTest { get; }

    /// <summary>Full, compact horizontal or compact vertical view.</summary>
    [ObservableProperty] private ViewLayout _layout;

    /// <summary>Background with the chosen transparency.</summary>
    [ObservableProperty] private string _backgroundColor = AppearanceSettings.DefaultBackgroundColor;
    /// <summary>Background without transparency, used by the details tooltip.</summary>
    [ObservableProperty] private string _solidBackgroundColor = AppearanceSettings.DefaultBackgroundColor;
    /// <summary>Main text color.</summary>
    [ObservableProperty] private string _textColor = AppearanceSettings.DefaultTextColor;
    /// <summary>Faded text color for labels.</summary>
    [ObservableProperty] private string _labelColor = AppearanceSettings.DefaultTextColor;
    /// <summary>Color of dividers and empty bar tracks.</summary>
    [ObservableProperty] private string _dividerColor = AppearanceSettings.DefaultTextColor;
    /// <summary>Text weight name as WPF expects it ("Normal", "SemiBold"...).</summary>
    [ObservableProperty] private string _fontWeightName = nameof(TextWeight.Normal);
    /// <summary>Widget zoom; 1 = 100%.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetCornerRadius))]
    [NotifyPropertyChangedFor(nameof(DividerThickness))]
    private double _scale = 1;

    /// <summary>Corner radius before zoom.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetCornerRadius))]
    private double _cornerRadius;

    /// <summary>Corner radius of the usage bars.</summary>
    [ObservableProperty] private double _barRadius;

    /// <summary>Border thickness; 0 when the border is off.</summary>
    [ObservableProperty] private double _borderWidth;
    /// <summary>Border color including its transparency.</summary>
    [ObservableProperty] private string _borderColor = AppearanceSettings.DefaultTextColor;
    /// <summary>Shows the border only while the mouse is over the widget.</summary>
    [ObservableProperty] private bool _borderOnHover;

    /// <summary>CPU bar color, following its usage level.</summary>
    [ObservableProperty] private string _cpuBarColor = AccentColor;
    /// <summary>CPU percentage color.</summary>
    [ObservableProperty] private string _cpuTextColor = AppearanceSettings.DefaultTextColor;
    /// <summary>GPU bar color.</summary>
    [ObservableProperty] private string _gpuBarColor = AccentColor;
    /// <summary>GPU percentage color.</summary>
    [ObservableProperty] private string _gpuTextColor = AppearanceSettings.DefaultTextColor;
    /// <summary>RAM bar color.</summary>
    [ObservableProperty] private string _memoryBarColor = AccentColor;
    /// <summary>RAM percentage color.</summary>
    [ObservableProperty] private string _memoryTextColor = AppearanceSettings.DefaultTextColor;
    /// <summary>Disk bar color.</summary>
    [ObservableProperty] private string _diskBarColor = AccentColor;
    /// <summary>Disk percentage color.</summary>
    [ObservableProperty] private string _diskTextColor = AppearanceSettings.DefaultTextColor;
    /// <summary>Battery bar color.</summary>
    [ObservableProperty] private string _batteryBarColor = AccentColor;
    /// <summary>Battery percentage color.</summary>
    [ObservableProperty] private string _batteryTextColor = AppearanceSettings.DefaultTextColor;

    /// <summary>Items shown on the widget, in the user's order.</summary>
    [ObservableProperty] private IReadOnlyList<MetricItem> _visibleItems = [];
    /// <summary>Items shown in the details tooltip, including hidden ones.</summary>
    [ObservableProperty] private IReadOnlyList<MetricItem> _detailItems = [];

    /// <summary>Tray icon tooltip.</summary>
    [ObservableProperty] private string _trayText = TrayTooltip.Title;

    /// <summary>Shows the details tooltip when hovering the widget.</summary>
    [ObservableProperty] private bool _detailedTooltip;

    /// <summary>Shortened processor name for the widget.</summary>
    [ObservableProperty] private string _cpuName = DisplayFormat.Unavailable;

    /// <summary>Full processor name for the details.</summary>
    [ObservableProperty] private string _cpuFullName = DisplayFormat.Unavailable;

    /// <summary>CPU usage, 0–100, for the bar.</summary>
    [ObservableProperty] private double _cpuUsage;
    /// <summary>CPU usage level, which picks its color.</summary>
    [ObservableProperty] private UsageLevel _cpuLevel;
    /// <summary>CPU usage text.</summary>
    [ObservableProperty] private string _cpuUsageText = DisplayFormat.Unavailable;
    /// <summary>Current CPU clock text.</summary>
    [ObservableProperty] private string _cpuFrequencyText = DisplayFormat.Unavailable;

    /// <summary>RAM usage, 0–100, for the bar.</summary>
    [ObservableProperty] private double _memoryUsage;
    /// <summary>RAM usage level.</summary>
    [ObservableProperty] private UsageLevel _memoryLevel;
    /// <summary>RAM usage text.</summary>
    [ObservableProperty] private string _memoryUsageText = DisplayFormat.Unavailable;
    /// <summary>Used and total RAM.</summary>
    [ObservableProperty] private string _memoryAmountText = DisplayFormat.Unavailable;

    /// <summary>Shortened GPU name for the widget.</summary>
    [ObservableProperty] private string _gpuName = DisplayFormat.Unavailable;

    /// <summary>Full GPU name for the details.</summary>
    [ObservableProperty] private string _gpuFullName = DisplayFormat.Unavailable;

    /// <summary>GPU usage, 0–100, for the bar.</summary>
    [ObservableProperty] private double _gpuUsage;
    /// <summary>GPU usage level.</summary>
    [ObservableProperty] private UsageLevel _gpuLevel;
    /// <summary>GPU usage text.</summary>
    [ObservableProperty] private string _gpuUsageText = DisplayFormat.Unavailable;
    /// <summary>Short GPU memory text for the widget.</summary>
    [ObservableProperty] private string _gpuMemoryText = DisplayFormat.Unavailable;
    /// <summary>GPU memory text for the details.</summary>
    [ObservableProperty] private string _gpuMemoryDetailText = DisplayFormat.Unavailable;
    /// <summary>GPU temperature.</summary>
    [ObservableProperty] private string _gpuTemperatureText = DisplayFormat.Unavailable;
    /// <summary>GPU temperature line for the details.</summary>
    [ObservableProperty] private string _gpuTemperatureDetailText = DisplayFormat.Unavailable;
    /// <summary>Shows the GPU temperature on the widget: the option is on and the driver reports it.</summary>
    [ObservableProperty] private bool _showGpuTemperature;

    /// <summary>Label of the disk shown on the widget, or why it's missing.</summary>
    [ObservableProperty] private string _diskName = DisplayFormat.Unavailable;
    /// <summary>Disk active time, 0–100, for the bar.</summary>
    [ObservableProperty] private double _diskActive;
    /// <summary>Disk usage level.</summary>
    [ObservableProperty] private UsageLevel _diskLevel;
    /// <summary>Disk active time text.</summary>
    [ObservableProperty] private string _diskActiveText = DisplayFormat.Unavailable;
    /// <summary>One row per disk for the details.</summary>
    [ObservableProperty] private IReadOnlyList<DiskDetail> _diskDetails = [];
    /// <summary>Shows read and write speeds next to the disk.</summary>
    [ObservableProperty] private bool _showDiskSpeed;
    /// <summary>Read and write speeds of the disk shown.</summary>
    [ObservableProperty] private string _diskSpeedText = DisplayFormat.Unavailable;

    /// <summary>Battery charge, 0–100, for the bar.</summary>
    [ObservableProperty] private double _batteryCharge;
    /// <summary>Battery level: low charge is the warning.</summary>
    [ObservableProperty] private UsageLevel _batteryLevel;
    /// <summary>Battery charge text.</summary>
    [ObservableProperty] private string _batteryText = DisplayFormat.Unavailable;
    /// <summary>Shows the charging icon.</summary>
    [ObservableProperty] private bool _isBatteryCharging;
    /// <summary>Charging, plugged in or time left.</summary>
    [ObservableProperty] private string _batteryStateText = DisplayFormat.Unavailable;

    /// <summary>Frames per second.</summary>
    [ObservableProperty] private string _fpsText = DisplayFormat.Unavailable;
    /// <summary>Name of the process being measured.</summary>
    [ObservableProperty] private string _fpsProcessText = DisplayFormat.Unavailable;

    /// <summary>Name of the chosen network device; empty when showing all devices.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNetworkDeviceName))]
    private string _networkDeviceName = string.Empty;

    /// <summary>True when a specific device is chosen.</summary>
    public bool HasNetworkDeviceName => NetworkDeviceName.Length > 0;

    /// <summary>Download speed.</summary>
    [ObservableProperty] private string _downloadSpeedText = DisplayFormat.Unavailable;
    /// <summary>Upload speed.</summary>
    [ObservableProperty] private string _uploadSpeedText = DisplayFormat.Unavailable;
    /// <summary>Data received and sent since the session started.</summary>
    [ObservableProperty] private string _networkSessionText = DisplayFormat.Unavailable;

    /// <summary>Latest ping result.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingLabelText), nameof(PingDetailText))]
    private string _pingText = DisplayFormat.Unavailable;

    /// <summary>Host being pinged.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingDetailText))]
    private string _pingHost = string.Empty;

    /// <summary>Shows the ping.</summary>
    [ObservableProperty] private bool _pingEnabled;

    /// <summary>Ping text for the widget.</summary>
    public string PingLabelText => Localizer.Format(Strings.Format_Ping, PingText);

    /// <summary>Ping text with the host, for the details.</summary>
    public string PingDetailText => Localizer.Format(Strings.Format_PingDetail, PingText, PingHost);

    /// <summary>Corner radius of the outer border, zoomed by hand because the border sits outside the zoomed content.</summary>
    public double WidgetCornerRadius => CornerRadius * Scale;

    /// <summary>Divider thickness that stays a whole number of pixels after zoom, so dividers stay sharp.</summary>
    public double DividerThickness => Math.Max(1, Math.Round(Scale, MidpointRounding.AwayFromZero)) / Scale;

    /// <summary>Disks from the latest reading, for the settings window.</summary>
    public IReadOnlyList<DiskActivity> AvailableDisks => _lastSnapshot?.Disk.Disks ?? [];

    /// <summary>GPUs from the latest reading, for the settings window.</summary>
    public IReadOnlyList<GpuActivity> AvailableGpus => _lastSnapshot?.Gpu.Gpus ?? [];

    /// <summary>True when the latest reading found a battery.</summary>
    public bool HasBattery => _hasBattery;

    /// <summary>Hidden text that reserves the width of the disk speeds.</summary>
    public string DiskSpeedWidestText => DisplayFormat.DiskRatesWidest;

    /// <summary>Hidden text that reserves the width of the network speeds.</summary>
    public string NetworkSpeedWidestText => DisplayFormat.NetworkRateWidest;

    /// <summary>Applies saved settings and redraws with the latest reading.</summary>
    /// <param name="settings">Settings now in effect.</param>
    public void UpdateSettings(SavedSettings settings)
    {
        _display = settings.Display;
        _colors = settings.Colors;
        _networkDevice = settings.Network.DefaultDevice;
        ApplyDisplay();
        ApplyAppearance(settings.Appearance);

        // Clears the old result so a ping from the previous host isn't shown.
        if (PingHost != settings.Network.PingHost || PingEnabled != settings.Network.PingEnabled)
        {
            PingHost = settings.Network.PingHost;
            PingEnabled = settings.Network.PingEnabled;
            ApplyPing(null);
        }

        if (_lastSnapshot is not null)
        {
            Apply(_lastSnapshot);
        }
    }

    /// <summary>Shows appearance changes that aren't saved yet.</summary>
    /// <param name="appearance">Appearance to preview.</param>
    /// <param name="colors">Usage colors to preview.</param>
    public void PreviewAppearance(AppearanceSettings appearance, ColorSettings colors)
    {
        _colors = colors;
        ApplyAppearance(appearance);

        if (_lastSnapshot is not null)
        {
            Apply(_lastSnapshot);
        }
    }

    /// <summary>Shows a new reading.</summary>
    /// <param name="snapshot">Reading taken by the hardware monitor.</param>
    public void Apply(HardwareSnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        CpuName = DisplayFormat.ShortName(snapshot.Cpu.Name);
        CpuFullName = snapshot.Cpu.Name ?? DisplayFormat.Unavailable;
        CpuUsage = snapshot.Cpu.UsagePercent ?? 0;
        CpuLevel = UsageLevels.Classify(snapshot.Cpu.UsagePercent, _colors.Cpu, _colors.Enabled);
        CpuUsageText = DisplayFormat.Percent(snapshot.Cpu.UsagePercent);
        CpuFrequencyText = DisplayFormat.Gigahertz(snapshot.Cpu.FrequencyGhz);

        MemoryUsage = snapshot.Memory?.UsagePercent ?? 0;
        MemoryLevel = UsageLevels.Classify(snapshot.Memory?.UsagePercent, _colors.Memory, _colors.Enabled);
        MemoryUsageText = DisplayFormat.Percent(snapshot.Memory?.UsagePercent);
        MemoryAmountText = DisplayFormat.UsedOfTotal(snapshot.Memory?.UsedBytes, snapshot.Memory?.TotalBytes);

        ApplyGpu(snapshot.Gpu);
        ApplyBattery(snapshot.Battery);
        ApplyDisk(snapshot.Disk);

        ApplyNetwork(snapshot.Network);
        RefreshTrayText();
    }

    // With a chosen device, shows its traffic or "disconnected"; otherwise the total of all devices.
    private void ApplyNetwork(NetworkSnapshot network)
    {
        if (_networkDevice is null)
        {
            NetworkDeviceName = string.Empty;
            ShowNetwork(network.DownloadBytesPerSecond, network.UploadBytesPerSecond, network.SessionReceivedBytes, network.SessionSentBytes);
            return;
        }

        DeviceTraffic? traffic = network.Find(_networkDevice.Id);
        bool isUp = traffic?.IsUp == true;
        string name = traffic?.Name ?? _networkDevice.Name;

        NetworkDeviceName = isUp ? name : Localizer.Format(Strings.Format_Disconnected, name);
        ShowNetwork(
            isUp ? traffic!.DownloadBytesPerSecond : null,
            isUp ? traffic!.UploadBytesPerSecond : null,
            traffic?.SessionReceivedBytes ?? 0,
            traffic?.SessionSentBytes ?? 0);
    }

    private void ShowNetwork(double? downloadBytesPerSecond, double? uploadBytesPerSecond, ulong sessionReceivedBytes, ulong sessionSentBytes)
    {
        _downloadBytesPerSecond = downloadBytesPerSecond;
        _uploadBytesPerSecond = uploadBytesPerSecond;
        DownloadSpeedText = DisplayFormat.NetworkRate(downloadBytesPerSecond);
        UploadSpeedText = DisplayFormat.NetworkRate(uploadBytesPerSecond);
        NetworkSessionText = Localizer.Format(
            Strings.Format_Session,
            DisplayFormat.Size(sessionReceivedBytes),
            DisplayFormat.Size(sessionSentBytes));
    }

    /// <summary>Shows the latest FPS reading; the FPS row appears and disappears with it.</summary>
    /// <param name="snapshot">FPS reading, or null when there is nothing to measure.</param>
    public void ApplyFps(FpsSnapshot? snapshot)
    {
        _lastFps = snapshot;
        FpsText = DisplayFormat.FramesPerSecond(snapshot?.FramesPerSecond);
        FpsProcessText = snapshot?.ProcessName ?? DisplayFormat.Unavailable;

        if (IsFpsVisible != _fpsShown)
        {
            ApplyDisplay();
        }

        RefreshTrayText();
    }

    /// <summary>Shows the latest ping.</summary>
    /// <param name="roundTripMilliseconds">Round-trip time, or null when the host didn't answer.</param>
    public void ApplyPing(long? roundTripMilliseconds)
    {
        _lastPing = roundTripMilliseconds;
        PingText = DisplayFormat.Milliseconds(roundTripMilliseconds);
        RefreshTrayText();
    }

    /// <summary>Sets the zoom, e.g. while the user resizes the widget.</summary>
    /// <param name="percent">Zoom in percent; clamped to the allowed range.</param>
    public void ApplyScale(int percent) => Scale = ResizeRule.ClampPercent(percent) / 100.0;

    // FPS shows only when enabled and a reading exists.
    private bool IsFpsVisible => _display.ShowFps && _lastFps is not null;

    // Rebuilds every text after a language change.
    private void RefreshTexts()
    {
        ApplyPing(_lastPing);
        ApplyFps(_lastFps);
        OnPropertyChanged(nameof(PingLabelText));
        OnPropertyChanged(nameof(PingDetailText));
        OnPropertyChanged(nameof(DiskSpeedWidestText));
        OnPropertyChanged(nameof(NetworkSpeedWidestText));

        if (_lastSnapshot is not null)
        {
            Apply(_lastSnapshot);
        }
    }

    // Rebuilds the item lists: FPS always goes first, and the battery only on devices that have one.
    private void ApplyDisplay()
    {
        _fpsShown = IsFpsVisible;
        MetricItem[] top = _fpsShown ? [MetricItem.Fps] : [];
        MetricItem[] available = [.. _display.Order.Items.Where(item => item != MetricItem.Battery || _hasBattery)];
        VisibleItems = [.. top, .. available.Where(_display.IsShown)];
        DetailItems = [.. top, .. available];
        ShowDiskSpeed = _display.ShowDiskSpeed;
        DetailedTooltip = _display.DetailedTooltip;
        RefreshTrayText();
    }

    // Lines that don't fit the tray's 127-character limit are dropped from the end.
    private void RefreshTrayText() =>
        TrayText = TrayTooltip.Build([TrayTooltip.Title, .. VisibleItems.Select(TrayLine)]);

    // The tray shows network speeds as bare numbers to save space.
    private string TrayLine(MetricItem item) => item switch
    {
        MetricItem.Fps when FpsProcessText == DisplayFormat.Unavailable => Localizer.Format(Strings.Format_TrayValue, Strings.Metric_Fps, FpsText),
        MetricItem.Fps => Localizer.Format(Strings.Format_TrayLine, Strings.Metric_Fps, FpsText, FpsProcessText),
        MetricItem.Cpu => Localizer.Format(Strings.Format_TrayLine, Strings.Metric_Cpu, CpuUsageText, CpuFrequencyText),
        MetricItem.Gpu when ShowGpuTemperature => Localizer.Format(Strings.Format_TrayLineExtra, Strings.Metric_Gpu, GpuUsageText, GpuMemoryText, GpuTemperatureText),
        MetricItem.Gpu => Localizer.Format(Strings.Format_TrayLine, Strings.Metric_Gpu, GpuUsageText, GpuMemoryText),
        MetricItem.Memory => Localizer.Format(Strings.Format_TrayLine, Strings.Metric_Memory, MemoryUsageText, MemoryAmountText),
        MetricItem.Disk when ShowDiskSpeed => Localizer.Format(Strings.Format_TrayLine, Strings.Metric_Disk, DiskActiveText, DiskSpeedText),
        MetricItem.Disk => Localizer.Format(Strings.Format_TrayValue, Strings.Metric_Disk, DiskActiveText),
        MetricItem.Battery => Localizer.Format(Strings.Format_TrayLine, Strings.Metric_Battery, BatteryText, BatteryStateText),
        _ when !PingEnabled => Localizer.Format(
            Strings.Format_TrayNetworkWithoutPing,
            Strings.Metric_Network,
            DisplayFormat.Megabits(_downloadBytesPerSecond),
            DisplayFormat.Megabits(_uploadBytesPerSecond)),
        _ => Localizer.Format(
            Strings.Format_TrayNetwork,
            Strings.Metric_Network,
            DisplayFormat.Megabits(_downloadBytesPerSecond),
            DisplayFormat.Megabits(_uploadBytesPerSecond),
            PingText),
    };

    // Invalid saved values fall back to defaults or are clamped.
    private void ApplyAppearance(AppearanceSettings appearance)
    {
        string background = HexColor.Normalize(appearance.BackgroundColor, AppearanceSettings.DefaultBackgroundColor);
        string text = HexColor.Normalize(appearance.TextColor, AppearanceSettings.DefaultTextColor);
        int transparency = Math.Clamp(appearance.Transparency, 0, AppearanceSettings.MaximumTransparency);

        SolidBackgroundColor = background;
        BackgroundColor = HexColor.WithAlpha(background, BackgroundAlpha(transparency));
        TextColor = text;
        LabelColor = HexColor.WithAlpha(text, LabelAlpha);
        DividerColor = HexColor.WithAlpha(text, DividerAlpha);
        FontWeightName = Enum.IsDefined(appearance.TextWeight) ? appearance.TextWeight.ToString() : nameof(TextWeight.Normal);
        ApplyScale(appearance.Scale);
        CornerRadius = CornerRule.Clamp(appearance.CornerRadius);
        BarRadius = Math.Clamp(appearance.BarRadius, 0, AppearanceSettings.MaximumBarRadius);
        ApplyBorder(appearance);

        ApplyLevelColors();
    }

    private void ApplyBorder(AppearanceSettings appearance)
    {
        BorderMode mode = Enum.IsDefined(appearance.BorderMode) ? appearance.BorderMode : BorderMode.Off;
        int thickness = Math.Clamp(appearance.BorderThickness, AppearanceSettings.MinimumBorderThickness, AppearanceSettings.MaximumBorderThickness);
        int transparency = Math.Clamp(appearance.BorderTransparency, 0, AppearanceSettings.MaximumTransparency);
        string color = HexColor.Normalize(appearance.BorderColor, AppearanceSettings.DefaultTextColor);

        BorderWidth = mode == BorderMode.Off ? 0 : thickness;
        BorderColor = HexColor.WithAlpha(color, Opacity(transparency));
        BorderOnHover = mode == BorderMode.Hover;
    }

    // Transparency 0 = opaque.
    private static byte Opacity(int transparency) =>
        (byte)Math.Round(255 * (AppearanceSettings.MaximumTransparency - transparency) / (double)AppearanceSettings.MaximumTransparency);

    // Never fully transparent: fully transparent pixels ignore the mouse.
    private static byte BackgroundAlpha(int transparency) => Math.Max((byte)1, Opacity(transparency));

    // Recolors every item after the usage colors change.
    private void ApplyLevelColors()
    {
        (CpuBarColor, CpuTextColor) = LevelColors(CpuLevel);
        (GpuBarColor, GpuTextColor) = LevelColors(GpuLevel);
        (MemoryBarColor, MemoryTextColor) = LevelColors(MemoryLevel);
        (DiskBarColor, DiskTextColor) = LevelColors(DiskLevel);
        (BatteryBarColor, BatteryTextColor) = LevelColors(BatteryLevel);
    }

    // Level None (usage colors off): accent bar and normal text.
    private (string Bar, string Text) LevelColors(UsageLevel level) => level switch
    {
        UsageLevel.Normal => Both(HexColor.Normalize(_colors.NormalColor, ColorSettings.DefaultNormalColor)),
        UsageLevel.Warning => Both(HexColor.Normalize(_colors.WarningColor, ColorSettings.DefaultWarningColor)),
        UsageLevel.Critical => Both(HexColor.Normalize(_colors.CriticalColor, ColorSettings.DefaultCriticalColor)),
        _ => (AccentColor, TextColor),
    };

    private static (string Bar, string Text) Both(string color) => (color, color);

    // Each level change recolors its own item.
    partial void OnCpuLevelChanged(UsageLevel value) => (CpuBarColor, CpuTextColor) = LevelColors(value);

    partial void OnGpuLevelChanged(UsageLevel value) => (GpuBarColor, GpuTextColor) = LevelColors(value);

    partial void OnMemoryLevelChanged(UsageLevel value) => (MemoryBarColor, MemoryTextColor) = LevelColors(value);

    partial void OnDiskLevelChanged(UsageLevel value) => (DiskBarColor, DiskTextColor) = LevelColors(value);

    partial void OnBatteryLevelChanged(UsageLevel value) => (BatteryBarColor, BatteryTextColor) = LevelColors(value);

    // The battery row appears or disappears when a battery is connected or removed.
    private void ApplyBattery(BatterySnapshot? battery)
    {
        BatteryCharge = battery?.Percent ?? 0;
        BatteryLevel = UsageLevels.ClassifyBattery(battery?.Percent, _colors.Enabled);
        BatteryText = DisplayFormat.BatteryPercent(battery);
        IsBatteryCharging = battery?.IsCharging == true;
        BatteryStateText = DisplayFormat.BatteryState(battery);

        if ((battery is not null) != _hasBattery)
        {
            _hasBattery = battery is not null;
            ApplyDisplay();
        }
    }

    // The chosen GPU, or Windows' high-performance GPU when the choice is "Automatic" or the GPU is gone.
    private void ApplyGpu(GpuSnapshot gpus)
    {
        GpuActivity? gpu = (_display.Gpu is GpuChoice choice ? gpus.Find(choice.Id) : null) ?? gpus.Preferred;

        GpuName = DisplayFormat.ShortName(gpu?.Name);
        GpuFullName = gpu?.Name ?? DisplayFormat.Unavailable;
        GpuUsage = gpu?.UsagePercent ?? 0;
        GpuLevel = UsageLevels.Classify(gpu?.UsagePercent, _colors.Gpu, _colors.Enabled);
        GpuUsageText = DisplayFormat.Percent(gpu?.UsagePercent);
        GpuMemoryText = DisplayFormat.GpuMemory(gpu);
        GpuMemoryDetailText = DisplayFormat.GpuMemoryDetail(gpu);
        GpuTemperatureText = DisplayFormat.Temperature(gpu?.TemperatureCelsius);
        GpuTemperatureDetailText = Localizer.Format(Strings.Format_GpuTemperature, GpuTemperatureText);
        ShowGpuTemperature = _display.ShowGpuTemperature && gpu?.TemperatureCelsius is not null;
    }

    // The system disk or the chosen one; the details always list every disk.
    private void ApplyDisk(DiskSnapshot disks)
    {
        DiskActivity? disk = _display.DiskSource switch
        {
            DiskSource.System => disks.System,
            _ => _display.DiskNumber is int number ? disks.Find(number) : null,
        };

        DiskName = disk is not null ? DisplayFormat.DiskLabel(disk) : MissingDiskName(disks);
        DiskActive = disk?.ActivePercent ?? 0;
        DiskLevel = UsageLevels.Classify(disk?.ActivePercent, _colors.Disk, _colors.Enabled);
        DiskActiveText = DisplayFormat.Percent(disk?.ActivePercent);
        DiskSpeedText = DisplayFormat.DiskRates(disk?.ReadBytesPerSecond, disk?.WriteBytesPerSecond);
        DiskDetails = disks.Disks
            .Select(item => new DiskDetail(
                Localizer.Format(Strings.Format_DiskNumber, DisplayFormat.DiskLabel(item)),
                DisplayFormat.Percent(item.ActivePercent),
                DisplayFormat.DiskRates(item.ReadBytesPerSecond, item.WriteBytesPerSecond)))
            .ToList();
    }

    // Explains why the chosen disk has no reading.
    private string MissingDiskName(DiskSnapshot disks)
    {
        if (disks.Disks.Count == 0)
        {
            return DisplayFormat.Unavailable;
        }

        return _display.DiskSource switch
        {
            DiskSource.System => Strings.Disk_SystemNotFound,
            DiskSource.Specific when _display.DiskNumber is int number => Localizer.Format(Strings.Format_DiskNotFound, number),
            _ => DisplayFormat.Unavailable,
        };
    }
}

/// <summary>One disk row in the details.</summary>
/// <param name="Name">Disk label.</param>
/// <param name="ActiveText">Active time.</param>
/// <param name="SpeedText">Read and write speeds.</param>
public sealed record DiskDetail(string Name, string ActiveText, string SpeedText);
