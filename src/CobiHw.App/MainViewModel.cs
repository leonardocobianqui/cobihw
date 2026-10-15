using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using CobiHw.Core;
using CobiHw.Core.Fps;

namespace CobiHw.App;

public sealed partial class MainViewModel : ObservableObject
{
    private const string AccentColor = "#4FB0EA";
    private const byte LabelAlpha = 0xA6;
    private const byte DividerAlpha = 0x33;

    private DisplaySettings _display;
    private ColorSettings _colors;
    private HardwareSnapshot? _lastSnapshot;
    private FpsSnapshot? _lastFps;
    private bool _fpsShown;
    private long? _lastPing;

    public MainViewModel(SavedSettings settings, SpeedTestViewModel speedTest)
    {
        _display = settings.Display;
        _colors = settings.Colors;
        SpeedTest = speedTest;
        PingHost = settings.Network.PingHost;
        ApplyDisplay();
        ApplyAppearance(settings.Appearance);
        Localizer.Instance.CultureChanged += (_, _) => RefreshTexts();
    }

    public SpeedTestViewModel SpeedTest { get; }

    [ObservableProperty] private ViewLayout _layout;

    [ObservableProperty] private string _backgroundColor = AppearanceSettings.DefaultBackgroundColor;
    [ObservableProperty] private string _solidBackgroundColor = AppearanceSettings.DefaultBackgroundColor;
    [ObservableProperty] private string _textColor = AppearanceSettings.DefaultTextColor;
    [ObservableProperty] private string _labelColor = AppearanceSettings.DefaultTextColor;
    [ObservableProperty] private string _dividerColor = AppearanceSettings.DefaultTextColor;
    [ObservableProperty] private string _fontWeightName = nameof(TextWeight.Normal);
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetCornerRadius))]
    private double _scale = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidgetCornerRadius))]
    private double _cornerRadius;

    [ObservableProperty] private double _borderWidth;
    [ObservableProperty] private string _borderColor = AppearanceSettings.DefaultTextColor;
    [ObservableProperty] private bool _borderOnHover;

    [ObservableProperty] private string _cpuBarColor = AccentColor;
    [ObservableProperty] private string _cpuTextColor = AppearanceSettings.DefaultTextColor;
    [ObservableProperty] private string _gpuBarColor = AccentColor;
    [ObservableProperty] private string _gpuTextColor = AppearanceSettings.DefaultTextColor;
    [ObservableProperty] private string _memoryBarColor = AccentColor;
    [ObservableProperty] private string _memoryTextColor = AppearanceSettings.DefaultTextColor;
    [ObservableProperty] private string _diskBarColor = AccentColor;
    [ObservableProperty] private string _diskTextColor = AppearanceSettings.DefaultTextColor;

    [ObservableProperty] private IReadOnlyList<MetricItem> _visibleItems = [];
    [ObservableProperty] private IReadOnlyList<MetricItem> _detailItems = [];

    [ObservableProperty] private bool _detailedTooltip;

    [ObservableProperty] private string _cpuName = DisplayFormat.Unavailable;

    [ObservableProperty] private string _cpuFullName = DisplayFormat.Unavailable;

    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private UsageLevel _cpuLevel;
    [ObservableProperty] private PercentText _cpuUsageText = DisplayFormat.PercentParts(null);
    [ObservableProperty] private string _cpuFrequencyText = DisplayFormat.Unavailable;

    [ObservableProperty] private double _memoryUsage;
    [ObservableProperty] private UsageLevel _memoryLevel;
    [ObservableProperty] private PercentText _memoryUsageText = DisplayFormat.PercentParts(null);
    [ObservableProperty] private string _memoryAmountText = DisplayFormat.Unavailable;

    [ObservableProperty] private string _gpuName = DisplayFormat.Unavailable;

    [ObservableProperty] private string _gpuFullName = DisplayFormat.Unavailable;

    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private UsageLevel _gpuLevel;
    [ObservableProperty] private PercentText _gpuUsageText = DisplayFormat.PercentParts(null);
    [ObservableProperty] private string _gpuMemoryText = DisplayFormat.Unavailable;

    [ObservableProperty] private string _diskName = DisplayFormat.Unavailable;
    [ObservableProperty] private double _diskActive;
    [ObservableProperty] private UsageLevel _diskLevel;
    [ObservableProperty] private PercentText _diskActiveText = DisplayFormat.PercentParts(null);
    [ObservableProperty] private IReadOnlyList<DiskDetail> _diskDetails = [];

    [ObservableProperty] private string _fpsText = DisplayFormat.Unavailable;
    [ObservableProperty] private string _fpsProcessText = DisplayFormat.Unavailable;

    [ObservableProperty] private string _downloadSpeedText = DisplayFormat.Unavailable;
    [ObservableProperty] private string _uploadSpeedText = DisplayFormat.Unavailable;
    [ObservableProperty] private string _networkSessionText = DisplayFormat.Unavailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingLabelText), nameof(PingDetailText))]
    private string _pingText = DisplayFormat.Unavailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PingDetailText))]
    private string _pingHost = string.Empty;

    public string PingLabelText => Localizer.Format(Strings.Format_Ping, PingText);

    public string PingDetailText => Localizer.Format(Strings.Format_PingDetail, PingText, PingHost);

    public double WidgetCornerRadius => CornerRadius * Scale;

    public IReadOnlyList<DiskActivity> AvailableDisks => _lastSnapshot?.Disk.Disks ?? [];

    public void UpdateSettings(SavedSettings settings)
    {
        _display = settings.Display;
        _colors = settings.Colors;
        ApplyDisplay();
        ApplyAppearance(settings.Appearance);

        if (PingHost != settings.Network.PingHost)
        {
            PingHost = settings.Network.PingHost;
            ApplyPing(null);
        }

        if (_lastSnapshot is not null)
        {
            Apply(_lastSnapshot);
        }
    }

    public void Apply(HardwareSnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        CpuName = DisplayFormat.ShortName(snapshot.Cpu.Name);
        CpuFullName = snapshot.Cpu.Name ?? DisplayFormat.Unavailable;
        CpuUsage = snapshot.Cpu.UsagePercent ?? 0;
        CpuLevel = UsageLevels.Classify(snapshot.Cpu.UsagePercent, _colors.Cpu, _colors.Enabled);
        CpuUsageText = DisplayFormat.PercentParts(snapshot.Cpu.UsagePercent);
        CpuFrequencyText = DisplayFormat.Gigahertz(snapshot.Cpu.FrequencyGhz);

        MemoryUsage = snapshot.Memory?.UsagePercent ?? 0;
        MemoryLevel = UsageLevels.Classify(snapshot.Memory?.UsagePercent, _colors.Memory, _colors.Enabled);
        MemoryUsageText = DisplayFormat.PercentParts(snapshot.Memory?.UsagePercent);
        MemoryAmountText = DisplayFormat.UsedOfTotal(snapshot.Memory?.UsedBytes, snapshot.Memory?.TotalBytes);

        GpuName = DisplayFormat.ShortName(snapshot.Gpu.Name);
        GpuFullName = snapshot.Gpu.Name ?? DisplayFormat.Unavailable;
        GpuUsage = snapshot.Gpu.UsagePercent ?? 0;
        GpuLevel = UsageLevels.Classify(snapshot.Gpu.UsagePercent, _colors.Gpu, _colors.Enabled);
        GpuUsageText = DisplayFormat.PercentParts(snapshot.Gpu.UsagePercent);
        GpuMemoryText = DisplayFormat.UsedOfTotal(snapshot.Gpu.MemoryUsedBytes, snapshot.Gpu.MemoryTotalBytes);

        ApplyDisk(snapshot.Disk);

        DownloadSpeedText = DisplayFormat.MegabitsPerSecond(snapshot.Network.DownloadBytesPerSecond);
        UploadSpeedText = DisplayFormat.MegabitsPerSecond(snapshot.Network.UploadBytesPerSecond);
        NetworkSessionText = Localizer.Format(
            Strings.Format_Session,
            DisplayFormat.Size(snapshot.Network.SessionReceivedBytes),
            DisplayFormat.Size(snapshot.Network.SessionSentBytes));
    }

    public void ApplyFps(FpsSnapshot? snapshot)
    {
        _lastFps = snapshot;
        FpsText = DisplayFormat.FramesPerSecond(snapshot?.FramesPerSecond);
        FpsProcessText = snapshot?.ProcessName ?? DisplayFormat.Unavailable;

        if (IsFpsVisible != _fpsShown)
        {
            ApplyDisplay();
        }
    }

    public void ApplyPing(long? roundTripMilliseconds)
    {
        _lastPing = roundTripMilliseconds;
        PingText = DisplayFormat.Milliseconds(roundTripMilliseconds);
    }

    public void ApplyScale(int percent) => Scale = ResizeRule.ClampPercent(percent) / 100.0;

    private bool IsFpsVisible => _display.ShowFps && _lastFps is not null;

    private void RefreshTexts()
    {
        ApplyPing(_lastPing);
        ApplyFps(_lastFps);
        OnPropertyChanged(nameof(PingLabelText));
        OnPropertyChanged(nameof(PingDetailText));

        if (_lastSnapshot is not null)
        {
            Apply(_lastSnapshot);
        }
    }

    private void ApplyDisplay()
    {
        _fpsShown = IsFpsVisible;
        MetricItem[] top = _fpsShown ? [MetricItem.Fps] : [];
        VisibleItems = [.. top, .. _display.Order.Items.Where(_display.IsShown)];
        DetailItems = [.. top, .. _display.Order.Items];
        DetailedTooltip = _display.DetailedTooltip;
    }

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

    private static byte Opacity(int transparency) =>
        (byte)Math.Round(255 * (AppearanceSettings.MaximumTransparency - transparency) / (double)AppearanceSettings.MaximumTransparency);

    private static byte BackgroundAlpha(int transparency) => Math.Max((byte)1, Opacity(transparency));

    private void ApplyLevelColors()
    {
        (CpuBarColor, CpuTextColor) = LevelColors(CpuLevel);
        (GpuBarColor, GpuTextColor) = LevelColors(GpuLevel);
        (MemoryBarColor, MemoryTextColor) = LevelColors(MemoryLevel);
        (DiskBarColor, DiskTextColor) = LevelColors(DiskLevel);
    }

    private (string Bar, string Text) LevelColors(UsageLevel level) => level switch
    {
        UsageLevel.Normal => Both(HexColor.Normalize(_colors.NormalColor, ColorSettings.DefaultNormalColor)),
        UsageLevel.Warning => Both(HexColor.Normalize(_colors.WarningColor, ColorSettings.DefaultWarningColor)),
        UsageLevel.Critical => Both(HexColor.Normalize(_colors.CriticalColor, ColorSettings.DefaultCriticalColor)),
        _ => (AccentColor, TextColor),
    };

    private static (string Bar, string Text) Both(string color) => (color, color);

    partial void OnCpuLevelChanged(UsageLevel value) => (CpuBarColor, CpuTextColor) = LevelColors(value);

    partial void OnGpuLevelChanged(UsageLevel value) => (GpuBarColor, GpuTextColor) = LevelColors(value);

    partial void OnMemoryLevelChanged(UsageLevel value) => (MemoryBarColor, MemoryTextColor) = LevelColors(value);

    partial void OnDiskLevelChanged(UsageLevel value) => (DiskBarColor, DiskTextColor) = LevelColors(value);

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
        DiskActiveText = DisplayFormat.PercentParts(disk?.ActivePercent);
        DiskDetails = disks.Disks
            .Select(item => new DiskDetail(Localizer.Format(Strings.Format_DiskNumber, DisplayFormat.DiskLabel(item)), DisplayFormat.Percent(item.ActivePercent)))
            .ToList();
    }

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

public sealed record DiskDetail(string Name, string ActiveText);
