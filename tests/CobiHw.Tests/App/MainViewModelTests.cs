using CobiHw.App;
using CobiHw.App.Settings;
using CobiHw.Core;
using CobiHw.Core.Fps;
using CobiHw.Tests.Core;
using static CobiHw.Tests.TestData;

namespace CobiHw.Tests.App;

/// <summary>Widget state: disks, GPU, network, ping, FPS, battery, appearance, tray tooltip and languages.</summary>
[Collection(AppState.Name)]
public sealed class MainViewModelTests : LocalizedTest
{
    private static readonly Guid TailscaleId = Guid.Parse("37217669-42da-4657-a55b-0d995d328200");

    private static MainViewModel Showing(HardwareSnapshot snapshot, SavedSettings? settings = null)
    {
        MainViewModel widget = Widget(settings);
        widget.Apply(snapshot);
        return widget;
    }

    [Fact]
    public void Disk_DefaultIsTheWindowsDisk_EvenWhenAnotherIsBusier()
    {
        MainViewModel widget = Showing(Snapshot);

        Assert.Equal("0 (C:)", widget.DiskName);
        Assert.Equal("12%", widget.DiskActiveText);
        Assert.Equal(UsageLevel.Normal, widget.DiskLevel);
        Assert.Equal(3, widget.AvailableDisks.Count);
    }

    [Fact]
    public void Disk_SpecificDisk_IsAppliedRightAway()
    {
        MainViewModel widget = Showing(Snapshot);

        widget.UpdateSettings(Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 1 }));
        Assert.Equal("1 (D: E:)", widget.DiskName);
        Assert.Equal("80%", widget.DiskActiveText);
        Assert.Equal(UsageLevel.Critical, widget.DiskLevel);

        widget.UpdateSettings(Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 2 }));
        Assert.Equal("2", widget.DiskName);
        Assert.Equal("5%", widget.DiskActiveText);
    }

    [Fact]
    public void Disk_DetailsListEveryDisk()
    {
        MainViewModel widget = Showing(Snapshot);

        Assert.Equal(3, widget.DiskDetails.Count);
        Assert.Equal(new DiskDetail("Disco 0 (C:)", "12%", "—"), widget.DiskDetails[0]);
    }

    [Fact]
    public void Disk_MissingSpecificDisk_IsNotFoundAndHasNoColor()
    {
        MainViewModel widget = Showing(Snapshot, Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 9 }));

        Assert.Equal("9 (não encontrado)", widget.DiskName);
        Assert.Equal("—", widget.DiskActiveText);
        Assert.Equal(UsageLevel.None, widget.DiskLevel);
    }

    [Fact]
    public void Disk_WindowsDiskNotIdentified_SaysSo()
    {
        MainViewModel widget = Showing(Snapshot with { Disk = new DiskSnapshot([new DiskActivity(1, "D:", 3, false)]) });

        Assert.Equal("do Windows (não encontrado)", widget.DiskName);
        Assert.Equal("—", widget.DiskActiveText);
    }

    [Fact]
    public void Disk_NoDisksInTheCounter_IsADash()
    {
        MainViewModel widget = Showing(Snapshot with { Disk = new DiskSnapshot([]) });

        Assert.Equal("—", widget.DiskName);
        Assert.Equal("—", widget.DiskActiveText);
    }

    [Fact]
    public void Disk_Speed_OffByDefault_AlwaysInTheDetails()
    {
        var disks = new DiskSnapshot([new DiskActivity(0, "C:", 12, true, 3 * 1024 * 1024, 1024 * 1024), new DiskActivity(1, "D:", 80, false, 0, 0)]);
        MainViewModel widget = Showing(Snapshot with { Disk = disks });

        Assert.False(widget.ShowDiskSpeed);
        Assert.Contains("\nDisco 12%\n", widget.TrayText, StringComparison.Ordinal);
        Assert.Equal("L 3,00 MB/s · E 1,00 MB/s", widget.DiskDetails[0].SpeedText);
        Assert.Equal("L 0 KB/s · E 0 KB/s", widget.DiskDetails[1].SpeedText);

        widget.UpdateSettings(Settings(new DisplaySettings { ShowDiskSpeed = true }));

        Assert.True(widget.ShowDiskSpeed);
        Assert.Equal("L 3,00 MB/s · E 1,00 MB/s", widget.DiskSpeedText);
        Assert.Contains("\nDisco 12% · L 3,00 MB/s · E 1,00 MB/s", widget.TrayText, StringComparison.Ordinal);
    }

    [Fact]
    public void Items_HiddenOnesLeaveTheWidget_ButStayInTheDetails()
    {
        MainViewModel widget = Widget(Settings(new DisplaySettings { ShowMemory = false, DetailedTooltip = false }));

        Assert.DoesNotContain(MetricItem.Memory, widget.VisibleItems);
        Assert.Contains(MetricItem.Memory, widget.DetailItems);
        Assert.False(widget.DetailedTooltip);
    }

    [Fact]
    public void Items_DetailedTooltipIsOnByDefault()
    {
        Assert.True(Widget().DetailedTooltip);
        Assert.True(new DisplaySettings().DetailedTooltip);
    }

    [Fact]
    public void Items_FollowTheSavedOrder()
    {
        MainViewModel widget = Widget(Settings(new DisplaySettings { Order = new([MetricItem.Network, MetricItem.Disk]), ShowGpu = false }));

        Assert.Equal(new[] { MetricItem.Network, MetricItem.Disk, MetricItem.Cpu, MetricItem.Memory }, widget.VisibleItems);
        Assert.Equal(new[] { MetricItem.Network, MetricItem.Disk, MetricItem.Cpu, MetricItem.Gpu, MetricItem.Memory }, widget.DetailItems);
    }

    [Fact]
    public void Ping_StartsEmpty_AndClearsWhenTheHostChanges()
    {
        MainViewModel widget = Widget();

        Assert.Equal("8.8.8.8", widget.PingHost);
        Assert.Equal("—", widget.PingText);
        Assert.Equal("Ping — (8.8.8.8)", widget.PingDetailText);

        widget.ApplyPing(23);
        Assert.Equal("23 ms", widget.PingText);
        Assert.Equal("Ping 23 ms (8.8.8.8)", widget.PingDetailText);

        widget.ApplyPing(null);
        Assert.Equal("—", widget.PingText);

        // A result from the old host must not show next to the new one.
        widget.ApplyPing(15);
        widget.UpdateSettings(Settings(network: new NetworkSettings { PingHost = "1.1.1.1" }));
        Assert.Equal("1.1.1.1", widget.PingHost);
        Assert.Equal("—", widget.PingText);
    }

    [Fact]
    public void Ping_OffByDefault_TheTrayLineHasNoPing()
    {
        MainViewModel widget = Showing(Snapshot);
        widget.ApplyPing(23);

        Assert.False(widget.PingEnabled);
        Assert.Contains("\nRede ↓0,00 ↑0,00 Mbps", widget.TrayText, StringComparison.Ordinal);
        Assert.DoesNotContain("ping", widget.TrayText, StringComparison.Ordinal);

        widget.UpdateSettings(Settings(network: new NetworkSettings { PingEnabled = true }));

        Assert.True(widget.PingEnabled);
        Assert.Contains("· ping —", widget.TrayText, StringComparison.Ordinal);
    }

    [Fact]
    public void Appearance_Defaults()
    {
        MainViewModel widget = Widget();

        Assert.Equal("#FF1E1F22", widget.BackgroundColor);
        Assert.Equal("#1E1F22", widget.SolidBackgroundColor);
        Assert.Equal("#E6E6E6", widget.TextColor);
        Assert.Equal("#A6E6E6E6", widget.LabelColor);
        Assert.Equal("#33E6E6E6", widget.DividerColor);
        Assert.Equal("Normal", widget.FontWeightName);
    }

    [Fact]
    public void Appearance_TransparencyColorsAndWeight()
    {
        MainViewModel widget = Widget(Settings(appearance: new AppearanceSettings { Transparency = 50, BackgroundColor = "#000000", TextColor = "#00ff00", TextWeight = TextWeight.Bold }));

        Assert.Equal("#80000000", widget.BackgroundColor);
        Assert.Equal("#000000", widget.SolidBackgroundColor);
        Assert.Equal("#00FF00", widget.TextColor);
        Assert.Equal("Bold", widget.FontWeightName);
    }

    [Fact]
    public void Appearance_FullyTransparent_KeepsAlphaOneSoItStaysClickable() =>
        Assert.Equal("#011E1F22", Widget(Settings(appearance: new AppearanceSettings { Transparency = 100 })).BackgroundColor);

    [Fact]
    public void Appearance_InvalidSavedValues_FallBackToDefaults()
    {
        MainViewModel widget = Widget(Settings(appearance: new AppearanceSettings { Transparency = 400, BackgroundColor = "azul", TextWeight = (TextWeight)42 }));

        Assert.Equal("#011E1F22", widget.BackgroundColor);
        Assert.Equal("#1E1F22", widget.SolidBackgroundColor);
        Assert.Equal("Normal", widget.FontWeightName);
    }

    [Fact]
    public void UsageColors_FollowTheLevelOfEachItem()
    {
        MainViewModel widget = Showing(Snapshot);
        Assert.Equal("#6FD68A", widget.CpuBarColor);
        Assert.Equal("#6FD68A", widget.CpuTextColor);
        Assert.Equal("#6FD68A", widget.DiskTextColor);

        widget.UpdateSettings(Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 1 }, new ColorSettings { CriticalColor = "#0000ff", NormalColor = "#123456" }));
        Assert.Equal("#0000FF", widget.DiskBarColor);
        Assert.Equal("#0000FF", widget.DiskTextColor);
        Assert.Equal("#123456", widget.CpuTextColor);
    }

    [Fact]
    public void UsageColors_Off_AccentBarAndTextColorNumbers()
    {
        MainViewModel widget = Showing(Snapshot, Settings(colors: new ColorSettings { Enabled = false }, appearance: new AppearanceSettings { TextColor = "#FFFFFF" }));
        Assert.Equal("#4FB0EA", widget.CpuBarColor);
        Assert.Equal("#FFFFFF", widget.CpuTextColor);
        Assert.Equal(UsageLevel.None, widget.CpuLevel);

        // Same level, new text color: neutral numbers still follow it.
        widget.UpdateSettings(Settings(colors: new ColorSettings { Enabled = false }, appearance: new AppearanceSettings { TextColor = "#AAAAAA" }));
        Assert.Equal("#AAAAAA", widget.CpuTextColor);
        Assert.Equal("#AAAAAA", widget.GpuTextColor);
    }

    [Fact]
    public void Border_OffByDefault()
    {
        MainViewModel widget = Widget();

        Assert.Equal(0, widget.BorderWidth);
        Assert.False(widget.BorderOnHover);
    }

    [Fact]
    public void Border_AlwaysOrOnHover_WithLimitsAndTransparency()
    {
        MainViewModel widget = Widget(Settings(appearance: new AppearanceSettings { BorderMode = BorderMode.Always, BorderThickness = 3, BorderTransparency = 50, BorderColor = "#112233" }));
        Assert.Equal(3, widget.BorderWidth);
        Assert.Equal("#80112233", widget.BorderColor);
        Assert.False(widget.BorderOnHover);

        // Thickness is capped at 6 px; 100% transparent is alpha 0.
        widget.UpdateSettings(Settings(appearance: new AppearanceSettings { BorderMode = BorderMode.Hover, BorderThickness = 9, BorderTransparency = 100 }));
        Assert.Equal(6, widget.BorderWidth);
        Assert.True(widget.BorderOnHover);
        Assert.Equal("#00E6E6E6", widget.BorderColor);
    }

    [Fact]
    public void Border_InvalidInTheFile_IsOffWithTheDefaultColor()
    {
        MainViewModel widget = Widget(Settings(appearance: new AppearanceSettings { BorderMode = (BorderMode)42, BorderColor = "lixo" }));

        Assert.Equal(0, widget.BorderWidth);
        Assert.Equal("#80E6E6E6", widget.BorderColor);
    }

    [Fact]
    public void Scale_StartsAt100_IsLiveAndClamped()
    {
        MainViewModel widget = Widget();
        Assert.Equal(1, widget.Scale);

        widget.ApplyScale(150);
        Assert.Equal(1.5, widget.Scale);

        widget.ApplyScale(10);
        Assert.Equal(0.5, widget.Scale);

        widget.UpdateSettings(Settings(appearance: new AppearanceSettings { Scale = 80 }));
        Assert.Equal(0.8, widget.Scale);
    }

    [Fact]
    public void Corners_FollowTheScale_AndAreCapped()
    {
        // The outer border is outside the zoomed content, so its radius is zoomed by hand.
        MainViewModel widget = Widget(Settings(appearance: new AppearanceSettings { CornerRadius = 10, Scale = 150 }));
        Assert.Equal(10, widget.CornerRadius);
        Assert.Equal(15, widget.WidgetCornerRadius, 9);

        widget.ApplyScale(200);
        Assert.Equal(20, widget.WidgetCornerRadius, 9);

        widget.UpdateSettings(Settings(appearance: new AppearanceSettings { CornerRadius = 99 }));
        Assert.Equal(30, widget.CornerRadius);
    }

    [Fact]
    public void BarRadius_IsCappedAndPreviewed()
    {
        MainViewModel widget = Widget(Settings(appearance: new AppearanceSettings { BarRadius = 3 }));
        Assert.Equal(3, widget.BarRadius);
        Assert.Equal("#33E6E6E6", widget.DividerColor);

        widget.UpdateSettings(Settings(appearance: new AppearanceSettings { BarRadius = 50 }));
        Assert.Equal(3, widget.BarRadius);

        widget.PreviewAppearance(new AppearanceSettings { BarRadius = 0 }, new ColorSettings());
        Assert.Equal(0, widget.BarRadius);
    }

    [Theory]
    [InlineData(93, 1.0)] // 0.93 px before the fix.
    [InlineData(115, 1.0)]
    [InlineData(100, 1.0)]
    [InlineData(149, 1.0)]
    [InlineData(150, 2.0)]
    [InlineData(200, 2.0)]
    [InlineData(50, 1.0)] // Never zero.
    public void DividerThickness_IsAWholeNumberOfPixelsAfterZoom(int percent, double expectedPixels)
    {
        MainViewModel widget = Widget();

        widget.ApplyScale(percent);

        Assert.Equal(expectedPixels, widget.DividerThickness * widget.Scale, 9);
    }

    [Fact]
    public void DividerThickness_NotifiesWhenTheScaleChanges()
    {
        MainViewModel widget = Widget();
        int changes = 0;
        widget.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(MainViewModel.DividerThickness) ? 1 : 0;

        widget.ApplyScale(120);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Preview_ChangesTheWidgetUntilTheSavedSettingsComeBack()
    {
        MainViewModel widget = Showing(Snapshot);
        Assert.Equal("#FF1E1F22", widget.BackgroundColor);
        Assert.Equal(0, widget.WidgetCornerRadius);
        Assert.Equal(UsageLevel.Normal, widget.CpuLevel);

        widget.PreviewAppearance(new AppearanceSettings { Transparency = 50, CornerRadius = 4, Scale = 150 }, new ColorSettings { Cpu = new UsageThresholds(10, 12) });
        Assert.Equal("#801E1F22", widget.BackgroundColor);
        Assert.Equal(4, widget.CornerRadius);
        Assert.Equal(1.5, widget.Scale);
        Assert.Equal(UsageLevel.Critical, widget.CpuLevel);
        Assert.Equal("#FF6B63", widget.CpuBarColor);

        // The reading every second keeps the preview.
        widget.Apply(Snapshot);
        Assert.Equal(UsageLevel.Critical, widget.CpuLevel);
        Assert.Equal("#801E1F22", widget.BackgroundColor);

        // Cancel: back to what is saved.
        widget.UpdateSettings(Settings());
        Assert.Equal("#FF1E1F22", widget.BackgroundColor);
        Assert.Equal(0, widget.CornerRadius);
        Assert.Equal(1, widget.Scale);
        Assert.Equal(UsageLevel.Normal, widget.CpuLevel);
    }

    [Fact]
    public void Network_ChosenDevice_ShowsItsTraffic()
    {
        var traffic = new NetworkSnapshot(1_000_000, 0, 5, 6, [new DeviceTraffic(TailscaleId, "Tailscale", true, 250_000, 12_500, 1024, 2048, 1, 2)]);
        MainViewModel widget = Showing(Snapshot with { Network = traffic }, Settings(network: new NetworkSettings { DefaultDevice = new NetworkDeviceRef(TailscaleId, "Tailscale") }));

        Assert.Equal("Tailscale", widget.NetworkDeviceName);
        Assert.True(widget.HasNetworkDeviceName);
        Assert.Equal("2,00 Mbps", widget.DownloadSpeedText);
        Assert.Contains("1,00 KB", widget.NetworkSessionText, StringComparison.Ordinal);

        widget.Apply(Snapshot with { Network = traffic with { Devices = [new DeviceTraffic(TailscaleId, "Tailscale", false, null, null, 1024, 2048, 1, 2)] } });
        Assert.Equal("Tailscale (desconectado)", widget.NetworkDeviceName);
        Assert.Equal("—", widget.DownloadSpeedText);

        // Device gone: the saved name is shown with the warning.
        widget.Apply(Snapshot with { Network = traffic with { Devices = [] } });
        Assert.Equal("Tailscale (desconectado)", widget.NetworkDeviceName);
        Assert.Contains("0 B", widget.NetworkSessionText, StringComparison.Ordinal);

        // No device chosen: the sum of the physical adapters, without a name.
        widget.UpdateSettings(Settings());
        Assert.False(widget.HasNetworkDeviceName);
        Assert.Equal("8,00 Mbps", widget.DownloadSpeedText);
    }

    [Fact]
    public void Gpu_AutomaticChosenAndMissing()
    {
        MainViewModel widget = Showing(Snapshot with { Gpu = new GpuSnapshot([GpuTests.Intel, GpuTests.Nvidia]) });
        Assert.Equal("NVIDIA GeForce RTX 3060 Laptop GPU", widget.GpuFullName);
        Assert.Equal("40%", widget.GpuUsageText);
        Assert.Equal("2,3 / 6,0 GB", widget.GpuMemoryText);
        Assert.Equal("VRAM 2,3 / 6,0 GB", widget.GpuMemoryDetailText);

        widget.UpdateSettings(Settings(new DisplaySettings { Gpu = GpuChoice.Of(GpuTests.Intel) }));
        Assert.Equal("Intel(R) UHD Graphics 620", widget.GpuFullName);
        Assert.Equal("12%", widget.GpuUsageText);
        Assert.Equal("1,2 GB", widget.GpuMemoryText);

        // A chosen GPU that is gone falls back to automatic.
        widget.UpdateSettings(Settings(new DisplaySettings { Gpu = new GpuChoice(0x1002, 0x73DF, 0, "AMD Radeon RX 6700 XT") }));
        Assert.Equal("NVIDIA GeForce RTX 3060 Laptop GPU", widget.GpuFullName);

        widget.Apply(Snapshot with { Gpu = new GpuSnapshot([]) });
        Assert.Equal("—", widget.GpuName);
        Assert.Equal("—", widget.GpuUsageText);
        Assert.Equal("—", widget.GpuMemoryText);
    }

    [Fact]
    public void GpuTemperature_OffByDefault_AlwaysInTheDetails_HiddenWithoutAReading()
    {
        MainViewModel widget = Showing(Snapshot with { Gpu = new GpuSnapshot([GpuTests.Nvidia with { TemperatureCelsius = 46.9 }]) });
        Assert.False(widget.ShowGpuTemperature);
        Assert.Contains("\nGPU 40% · 2,3 / 6,0 GB\n", widget.TrayText, StringComparison.Ordinal);
        Assert.Equal("Temperatura 47 °C", widget.GpuTemperatureDetailText);

        widget.UpdateSettings(Settings(new DisplaySettings { ShowGpuTemperature = true }));
        Assert.True(widget.ShowGpuTemperature);
        Assert.Equal("47 °C", widget.GpuTemperatureText);
        Assert.Contains("\nGPU 40% · 2,3 / 6,0 GB · 47 °C\n", widget.TrayText, StringComparison.Ordinal);

        // On, but the driver reports no temperature.
        widget.Apply(Snapshot with { Gpu = new GpuSnapshot([GpuTests.Nvidia]) });
        Assert.False(widget.ShowGpuTemperature);
        Assert.Contains("\nGPU 40% · 2,3 / 6,0 GB\n", widget.TrayText, StringComparison.Ordinal);
        Assert.Equal("Temperatura —", widget.GpuTemperatureDetailText);
    }

    [Fact]
    public void Fps_Off_NotShownEvenWithFrames()
    {
        MainViewModel widget = Widget(Settings(new DisplaySettings { ShowGpu = false }));

        widget.ApplyFps(new FpsSnapshot(143.6, "jogo"));

        Assert.DoesNotContain(MetricItem.Fps, widget.VisibleItems);
        Assert.DoesNotContain(MetricItem.Fps, widget.DetailItems);
    }

    [Fact]
    public void Fps_On_IsTheFirstItem_WhileAGameIsMeasured()
    {
        MainViewModel widget = Widget(Settings(new DisplaySettings { ShowGpu = false, ShowFps = true, Order = new([MetricItem.Network, MetricItem.Disk]) }));

        widget.ApplyFps(new FpsSnapshot(143.6, "jogo"));
        Assert.Equal(new[] { MetricItem.Fps, MetricItem.Network, MetricItem.Disk, MetricItem.Cpu, MetricItem.Memory }, widget.VisibleItems);
        Assert.Equal(MetricItem.Fps, widget.DetailItems[0]);
        Assert.Equal(6, widget.DetailItems.Count);
        Assert.Equal("144", widget.FpsText);
        Assert.Equal("jogo", widget.FpsProcessText);

        widget.ApplyFps(null);
        Assert.DoesNotContain(MetricItem.Fps, widget.VisibleItems);
        Assert.Equal(MetricItem.Network, widget.VisibleItems[0]);
        Assert.Equal("—", widget.FpsText);

        widget.UpdateSettings(Settings(new DisplaySettings { ShowGpu = false }));
        widget.ApplyFps(new FpsSnapshot(60, null));
        Assert.DoesNotContain(MetricItem.Fps, widget.VisibleItems);
    }

    [Fact]
    public void Fps_ValueChanges_DoNotRebuildTheItemList()
    {
        MainViewModel widget = Widget(Settings(new DisplaySettings { ShowFps = true }));

        widget.ApplyFps(null);
        IReadOnlyList<MetricItem> withoutFps = widget.VisibleItems;
        widget.ApplyFps(null);
        Assert.Same(withoutFps, widget.VisibleItems);

        widget.ApplyFps(new FpsSnapshot(59.4, null));
        IReadOnlyList<MetricItem> withFps = widget.VisibleItems;
        widget.ApplyFps(new FpsSnapshot(60.2, null));
        Assert.Same(withFps, widget.VisibleItems);
        Assert.Equal("60", widget.FpsText);
        Assert.Equal("—", widget.FpsProcessText);
    }

    [Fact]
    public void Battery_AppearsOnlyWhenTheReadingHasOne()
    {
        MainViewModel widget = Showing(Snapshot);
        Assert.False(widget.HasBattery);
        Assert.DoesNotContain(MetricItem.Battery, widget.VisibleItems);
        Assert.DoesNotContain(MetricItem.Battery, widget.DetailItems);
        Assert.DoesNotContain("Bateria", widget.TrayText, StringComparison.Ordinal);

        widget.Apply(Snapshot with { Battery = new BatterySnapshot(85, true, true, null) });
        Assert.True(widget.HasBattery);
        Assert.Equal(MetricItem.Battery, widget.VisibleItems[^1]);
        Assert.Contains(MetricItem.Battery, widget.DetailItems);
        Assert.Equal("85%", widget.BatteryText);
        Assert.True(widget.IsBatteryCharging);
        Assert.Equal("Carregando", widget.BatteryStateText);
        Assert.Equal(85, widget.BatteryCharge);
        Assert.EndsWith("\nBateria 85% · Carregando", widget.TrayText, StringComparison.Ordinal);
    }

    [Fact]
    public void Battery_LowCharge_IsCritical_AndHidingKeepsItInTheDetails()
    {
        MainViewModel widget = Showing(Snapshot with { Battery = new BatterySnapshot(9, false, false, TimeSpan.FromMinutes(25)) });
        Assert.Equal(UsageLevel.Critical, widget.BatteryLevel);
        Assert.False(widget.IsBatteryCharging);
        Assert.Equal("Restam 0 h 25 min", widget.BatteryStateText);

        widget.UpdateSettings(Settings(new DisplaySettings { ShowBattery = false }));
        Assert.DoesNotContain(MetricItem.Battery, widget.VisibleItems);
        Assert.Contains(MetricItem.Battery, widget.DetailItems);
    }

    [Fact]
    public void TrayText_BeforeTheFirstReading_HasDashes() =>
        Assert.StartsWith("cobihw\nCPU — · —", Widget(Settings(network: new NetworkSettings { PingEnabled = true })).TrayText, StringComparison.Ordinal);

    [Fact]
    public void TrayText_ListsTheVisibleItems_InTheWidgetOrder()
    {
        MainViewModel widget = Showing(Snapshot, Settings(network: new NetworkSettings { PingEnabled = true }));
        widget.ApplyPing(23);

        Assert.Equal("cobihw\nCPU 13% · 2,60 GHz\nGPU 9% · 0,0 / 0,0 GB\nRAM 33% · 0,0 / 0,0 GB\nDisco 12%\nRede ↓0,00 ↑0,00 Mbps · ping 23 ms", widget.TrayText);

        widget.UpdateSettings(Settings(new DisplaySettings { ShowGpu = false, ShowFps = true, Order = new([MetricItem.Network, MetricItem.Cpu]) }, network: new NetworkSettings { PingEnabled = true }));
        widget.ApplyPing(23);
        widget.ApplyFps(new FpsSnapshot(144, "PathOfExile"));
        Assert.Equal("cobihw\nFPS 144 · PathOfExile\nRede ↓0,00 ↑0,00 Mbps · ping 23 ms\nCPU 13% · 2,60 GHz\nRAM 33% · 0,0 / 0,0 GB\nDisco 12%", widget.TrayText);

        widget.ApplyFps(new FpsSnapshot(60, null));
        Assert.Contains("\nFPS 60\n", widget.TrayText, StringComparison.Ordinal);

        widget.ApplyFps(null);
        Assert.DoesNotContain("FPS", widget.TrayText, StringComparison.Ordinal);

        SetLanguage("ru-RU");
        Assert.Contains("↓0,00 ↑0,00 Мбит/с · пинг 23", widget.TrayText, StringComparison.Ordinal);
    }

    [Fact]
    public void TrayText_WorstCase_StaysWithinTheWindowsLimit()
    {
        var wide = new HardwareSnapshot(
            new CpuSnapshot("x", 100, 4.6),
            new MemorySnapshot(17_000_000_000, 17_100_000_000),
            new GpuSnapshot([new GpuActivity(new GpuId(1, 2, 0), "y", 0, false, 100, 12_800_000_000, 12_880_000_000, null)]),
            new DiskSnapshot([new DiskActivity(0, "C:", 100, true)]),
            new NetworkSnapshot(124_000_000, 124_000_000, 0, 0, []));
        MainViewModel widget = Showing(wide, Settings(new DisplaySettings { ShowFps = true }));
        widget.ApplyPing(999);
        widget.ApplyFps(new FpsSnapshot(240, "UmJogoComNomeBemComprido"));

        Assert.True(widget.TrayText.Length <= 127, $"{widget.TrayText.Length} characters");
        Assert.StartsWith("cobihw\nFPS 240", widget.TrayText, StringComparison.Ordinal);

        SetLanguage("en-US");
        Assert.Contains("\nDisk 100%", widget.TrayText, StringComparison.Ordinal);
        Assert.Contains("\nCPU 100% · 4.60 GHz", widget.TrayText, StringComparison.Ordinal);
    }

    [Fact]
    public void Language_TextsRefreshRightAway()
    {
        MainViewModel widget = Showing(Snapshot);
        widget.ApplyPing(23);
        Assert.Equal("Ping 23 ms", widget.PingLabelText);

        SetLanguage("en-US");
        Assert.Equal("Ping 23 ms", widget.PingLabelText);
        Assert.Equal("2.60 GHz", widget.CpuFrequencyText);
        Assert.StartsWith("Session", widget.NetworkSessionText, StringComparison.Ordinal);
        Assert.Equal("Disk 0 (C:)", widget.DiskDetails[0].Name);

        SetLanguage("es-ES");
        Assert.Equal("Ping 23 ms", widget.PingLabelText);

        SetLanguage("ru-RU");
        Assert.Equal("Пинг 23 мс", widget.PingLabelText);
    }

    [Fact]
    public void WidestTexts_FollowTheLanguage_AndNotifyTheWindow()
    {
        MainViewModel widget = Widget();
        var changed = new List<string?>();
        widget.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        SetLanguage("en-US");

        Assert.Equal("R 99.9 MB/s · W 99.9 MB/s", widget.DiskSpeedWidestText);
        Assert.Equal("99.9 Mbps", widget.NetworkSpeedWidestText);
        Assert.Contains(nameof(MainViewModel.DiskSpeedWidestText), changed);
        Assert.Contains(nameof(MainViewModel.NetworkSpeedWidestText), changed);
    }
}
