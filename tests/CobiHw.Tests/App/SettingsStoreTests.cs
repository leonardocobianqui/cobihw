using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CobiHw.App;
using CobiHw.App.Settings;
using CobiHw.Core.Network;

namespace CobiHw.Tests.App;

/// <summary>Settings file: round trips, files from older versions, safe saving and one section per PC and user.</summary>
[Collection(AppState.Name)]
public sealed class SettingsStoreTests : LocalizedTest, IDisposable
{
    private readonly TempSettingsFile _file = new();

    private SettingsStore Store => _file.Store;

    /// <summary>Deletes the temporary settings folder.</summary>
    void IDisposable.Dispose() => _file.Dispose();

    [Fact]
    public void RoundTrip_LayoutSnapAndThresholds()
    {
        Store.Save(new AppSettings { Layout = ViewLayout.CompactVertical, SnapToEdges = false, Colors = new ColorSettings { Enabled = true, Disk = new UsageThresholds(30, 60) } });
        AppSettings loaded = Store.Load();

        Assert.Equal(ViewLayout.CompactVertical, loaded.Layout);
        Assert.False(loaded.SnapToEdges);
        Assert.Equal(new UsageThresholds(30, 60), loaded.Colors.Disk);
        Assert.Equal(UsageThresholds.Default, loaded.Colors.Cpu);
    }

    [Fact]
    public void OldFile_WithCompactView_OpensAndFillsTheDefaults()
    {
        _file.Write("{ \"Mode\": \"Desktop\", \"CompactView\": true, \"PositionLocked\": true }");
        AppSettings loaded = Store.Load();

        Assert.Equal(WidgetMode.Desktop, loaded.Mode);
        Assert.True(loaded.PositionLocked);
        Assert.Equal(ViewLayout.Full, loaded.Layout);
        Assert.True(loaded.SnapToEdges);
        Assert.True(loaded.Colors.Enabled);
    }

    [Fact]
    public void OldFile_FromThePreviousVersion_UsesTheNewDefaults()
    {
        _file.Write("{ \"Layout\": \"CompactHorizontal\", \"Display\": { \"ShowGpu\": false }, \"Colors\": { \"Enabled\": false } }");
        AppSettings loaded = Store.Load();

        Assert.Equal(DiskSource.System, loaded.Display.DiskSource);
        Assert.False(loaded.Display.ShowGpu);
        Assert.Equal("8.8.8.8", loaded.Network.PingHost);
        Assert.Null(loaded.LastSpeedTest);
        Assert.False(loaded.Colors.Enabled);
    }

    [Fact]
    public void RoundTrip_DiskPingAndLastSpeedTest()
    {
        var measured = new SpeedTestResult(new DateTimeOffset(2026, 10, 13, 17, 30, 0, TimeSpan.FromHours(-3)), 250e6, 48e6, 12.4, 2.1);
        Store.Save(new AppSettings { Display = new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 1 }, Network = new NetworkSettings { PingHost = "1.1.1.1" }, LastSpeedTest = measured });
        AppSettings loaded = Store.Load();

        Assert.Equal(DiskSource.Specific, loaded.Display.DiskSource);
        Assert.Equal(1, loaded.Display.DiskNumber);
        Assert.Equal("1.1.1.1", loaded.Network.PingHost);
        Assert.Equal(measured, loaded.LastSpeedTest);
    }

    [Fact]
    public void RoundTrip_Order()
    {
        Store.Save(new AppSettings { Display = new DisplaySettings { Order = new([MetricItem.Disk, MetricItem.Gpu]) } });
        Assert.Equal(new MetricOrder([MetricItem.Disk, MetricItem.Gpu]), Store.Load().Display.Order);
    }

    [Fact]
    public void OldFile_WithoutOrder_UsesTheDefault()
    {
        _file.Write("{ \"Display\": { \"ShowGpu\": false } }");
        Assert.Equal(MetricOrder.Default, Store.Load().Display.Order);
    }

    [Fact]
    public void File_WithIncompleteOrder_IsCompleted()
    {
        _file.Write("{ \"Display\": { \"Order\": { \"Items\": [\"Network\", \"Network\"] } } }");
        Assert.Equal(
            new[] { MetricItem.Network, MetricItem.Cpu, MetricItem.Gpu, MetricItem.Memory, MetricItem.Disk, MetricItem.Battery },
            Store.Load().Display.Order.Items);
    }

    [Fact]
    public void OldFile_WithOrderBeforeTheBattery_AddsTheBatteryAtTheEnd()
    {
        _file.Write("{ \"Display\": { \"Order\": { \"Items\": [\"Network\", \"Cpu\", \"Gpu\", \"Memory\", \"Disk\"] } } }");
        DisplaySettings display = Store.Load().Display;

        Assert.Equal(MetricItem.Battery, display.Order.Items[^1]);
        Assert.Equal(MetricItem.Network, display.Order.Items[0]);
        Assert.True(display.ShowBattery);
    }

    [Fact]
    public void RoundTrip_Appearance_WithTheWeightAsText()
    {
        var appearance = new AppearanceSettings { Transparency = 40, TextWeight = TextWeight.Light, TextColor = "#112233" };
        Store.Save(new AppSettings { Appearance = appearance, Colors = new ColorSettings { WarningColor = "#ABCDEF" } });
        AppSettings loaded = Store.Load();

        Assert.Equal(appearance, loaded.Appearance);
        Assert.Equal("#ABCDEF", loaded.Colors.WarningColor);
        Assert.Contains("\"Light\"", _file.Read());
    }

    [Fact]
    public void OldFile_WithoutAppearance_UsesTheDefaults()
    {
        _file.Write("{ \"Layout\": \"Full\" }");
        AppSettings loaded = Store.Load();

        Assert.Equal(new AppearanceSettings(), loaded.Appearance);
        Assert.Equal("#6FD68A", loaded.Colors.NormalColor);
    }

    [Fact]
    public void RoundTrip_Language_OldFileIsAutomatic()
    {
        Store.Save(new AppSettings { Language = "es-ES" });
        Assert.Equal("es-ES", Store.Load().Language);

        _file.Write("{ \"Layout\": \"Full\" }");
        Assert.Equal("auto", Store.Load().Language);
    }

    [Fact]
    public void RoundTrip_ClickThrough_WithTheKeyAsText()
    {
        Store.Save(new AppSettings { ClickThrough = true, InteractionKey = InteractionKey.Alt });
        AppSettings loaded = Store.Load();

        Assert.True(loaded.ClickThrough);
        Assert.Equal(InteractionKey.Alt, loaded.InteractionKey);
        Assert.Contains("\"Alt\"", _file.Read());
    }

    [Fact]
    public void RoundTrip_Scale_OldFileIs100()
    {
        _file.Write("{ \"Appearance\": { \"Transparency\": 10 } }");
        Assert.Equal(100, Store.Load().Appearance.Scale);

        Store.Save(new AppSettings { Appearance = new AppearanceSettings { Scale = 140 } });
        Assert.Equal(140, Store.Load().Appearance.Scale);
    }

    [Fact]
    public void RoundTrip_Border_WithTheModeAsText()
    {
        Store.Save(new AppSettings { Appearance = new AppearanceSettings { BorderMode = BorderMode.Hover } });

        Assert.Equal(BorderMode.Hover, Store.Load().Appearance.BorderMode);
        Assert.Contains("\"Hover\"", _file.Read());
    }

    [Fact]
    public void OldFile_WithClickThroughInDisplay_IsIgnoredWithoutError()
    {
        _file.Write("{ \"Display\": { \"ClickThrough\": true, \"InteractionKey\": \"Alt\" }, \"Appearance\": { \"Transparency\": 10 } }");
        AppSettings loaded = Store.Load();

        Assert.Equal(BorderMode.Off, loaded.Appearance.BorderMode);
        Assert.False(loaded.ClickThrough);
        Assert.Equal(InteractionKey.Ctrl, loaded.InteractionKey);
    }

    [Fact]
    public void RoundTrip_Theme_OldFileIsSystem()
    {
        Store.Save(new AppSettings { Theme = AppTheme.Dark });
        Assert.Equal(AppTheme.Dark, Store.Load().Theme);
        Assert.Contains("\"Dark\"", _file.Read());

        _file.Write("{ }");
        Assert.Equal(AppTheme.System, Store.Load().Theme);
    }

    [Fact]
    public void RoundTrip_SettingsWindowSize_OldFileHasNone()
    {
        Store.Save(new AppSettings { SettingsWidth = 520, SettingsHeight = 610.5 });
        AppSettings loaded = Store.Load();
        Assert.Equal(520, loaded.SettingsWidth);
        Assert.Equal(610.5, loaded.SettingsHeight);

        _file.Write("{ }");
        AppSettings old = Store.Load();
        Assert.Null(old.SettingsWidth);
        Assert.Null(old.SettingsHeight);
    }

    [Fact]
    public void RoundTrip_Fps_WithoutTheOldTaskPath()
    {
        Store.Save(new AppSettings { Display = new DisplaySettings { ShowFps = true } });

        Assert.True(Store.Load().Display.ShowFps);
        Assert.DoesNotContain("FpsTaskPath", _file.Read());
    }

    [Fact]
    public void OldFile_WithFpsTaskPath_IsReadWithoutError()
    {
        _file.Write("{ \"FpsTaskPath\": \"C:\\\\jogos\\\\cobihw.exe\" }");

        Assert.False(Store.Load().Display.ShowFps);
        Assert.False(Store.IsReadOnly);
    }

    [Fact]
    public void RoundTrip_Corners_OldFileIsSquare()
    {
        Store.Save(new AppSettings { Appearance = new AppearanceSettings { CornerRadius = 12 } });
        Assert.Equal(12, Store.Load().Appearance.CornerRadius);

        _file.Write("{ }");
        Assert.Equal(0, Store.Load().Appearance.CornerRadius);
    }

    [Fact]
    public void RoundTrip_BarRadius_OldFileIsSquare()
    {
        Store.Save(new AppSettings { Appearance = new AppearanceSettings { BarRadius = 2 } });
        Assert.Equal(2, Store.Load().Appearance.BarRadius);

        _file.Write("{ \"Appearance\": { \"CornerRadius\": 4 } }");
        Assert.Equal(0, Store.Load().Appearance.BarRadius);
    }

    [Fact]
    public void RoundTrip_NetworkDevicesAndDefault_OldFileSumsPhysicalAdapters()
    {
        Guid tailscale = Guid.NewGuid(), teredo = Guid.NewGuid();
        var choices = new NetworkDeviceChoices([new(tailscale, false), new(teredo, true)]);
        Store.Save(new AppSettings { Network = new NetworkSettings { Devices = choices, DefaultDevice = new NetworkDeviceRef(tailscale, "Tailscale") } });
        NetworkSettings loaded = Store.Load().Network;

        Assert.Equal(choices, loaded.Devices);
        Assert.Equal(new NetworkDeviceRef(tailscale, "Tailscale"), loaded.DefaultDevice);

        _file.Write("{ }");
        NetworkSettings old = Store.Load().Network;
        Assert.Null(old.DefaultDevice);
        Assert.Equal(NetworkDeviceChoices.Empty, old.Devices);
    }

    [Fact]
    public void OldFile_WithoutPingEnabled_HasPingOff() =>
        Assert.False(JsonSerializer.Deserialize<NetworkSettings>("{\"PingHost\":\"8.8.8.8\"}")?.PingEnabled);

    [Fact]
    public void GpuChoice_RoundTripsWithoutTheComputedId()
    {
        var choice = new GpuChoice(0x8086, 0x5917, 0, "Intel(R) UHD Graphics 620");
        string json = JsonSerializer.Serialize(new DisplaySettings { Gpu = choice });

        Assert.Equal(choice, JsonSerializer.Deserialize<DisplaySettings>(json)?.Gpu);
        Assert.DoesNotContain("\"Id\"", json);
        Assert.Contains("\"VendorId\":32902", json);
    }

    [Fact]
    public void Save_SkipsTheWriteWhenNothingChanged()
    {
        Assert.True(Store.Save(new AppSettings { Layout = ViewLayout.CompactVertical }));
        Assert.True(File.Exists(_file.FilePath));

        _file.Write("{ \"Layout\": \"Full\", \"marker\": 1 }");
        Assert.True(Store.Save(new AppSettings { Layout = ViewLayout.CompactVertical }));
        Assert.Contains("marker", _file.Read());

        Assert.True(Store.Save(new AppSettings { Layout = ViewLayout.Full }));
        Assert.DoesNotContain("marker", _file.Read());

        Assert.True(Store.Save(Store.Load()));
        Assert.False(File.Exists(_file.FilePath + ".tmp"));
    }

    [Fact]
    public void Save_WhenTheFileCantBeWritten_ReturnsFalseWithoutThrowing()
    {
        Store.Save(new AppSettings { Layout = ViewLayout.Full });

        // A folder where the temporary file goes blocks every write.
        Directory.CreateDirectory(_file.FilePath + ".tmp");
        Assert.False(Store.Save(new AppSettings { Layout = ViewLayout.CompactHorizontal }));

        var clock = Stopwatch.StartNew();
        Assert.False(Store.Save(new AppSettings { Layout = ViewLayout.CompactHorizontal }, TimeSpan.FromMilliseconds(600)));
        Assert.InRange(clock.ElapsedMilliseconds, 500, 2000);

        Directory.Delete(_file.FilePath + ".tmp");
        Assert.True(Store.Save(new AppSettings { Layout = ViewLayout.CompactHorizontal }));
        Assert.Contains("CompactHorizontal", _file.Read());
    }

    [Fact]
    public void Load_WhileAnotherProcessHoldsTheFile_IsReadOnlyUntilReleased()
    {
        Store.Save(new AppSettings { Layout = ViewLayout.CompactHorizontal });
        SettingsStore second = _file.For(TempSettingsFile.Profile);

        using (var holder = new FileStream(_file.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var clock = Stopwatch.StartNew();
            AppSettings fallback = second.Load(TimeSpan.FromMilliseconds(600));

            Assert.True(second.IsReadOnly);
            Assert.Equal(SettingsAccess.FileInUse, second.Access);
            Assert.Equal(ViewLayout.Full, fallback.Layout);
            Assert.True(clock.ElapsedMilliseconds >= 500, $"waited {clock.ElapsedMilliseconds} ms");
            Assert.False(second.Save(new AppSettings { Layout = ViewLayout.CompactVertical }));
            Assert.False(second.CanRead());
        }

        Assert.True(second.CanRead());
        Assert.Equal(ViewLayout.CompactHorizontal, second.Load().Layout);
        Assert.False(second.IsReadOnly);
    }

    [Fact]
    public void Load_CorruptFile_IsKeptAsInvalidAndDefaultsAreUsed()
    {
        _file.Write("{ this is not json");
        AppSettings loaded = Store.Load();

        Assert.True(File.Exists(_file.FilePath + ".invalid"));
        Assert.False(Store.IsReadOnly);
        Assert.Equal(ViewLayout.Full, loaded.Layout);
    }

    [Fact]
    public void LegacyPosition_IsReadForMigration_AndDroppedAfterSaving()
    {
        _file.Write("{ \"WindowLeft\": 653.5, \"WindowTop\": 1001 }");
        AppSettings legacy = Store.Load();
        Assert.Equal(653.5, legacy.WindowLeft);
        Assert.Equal(1001, legacy.WindowTop);
        Assert.Null(legacy.WindowX);

        legacy.WindowX = 980;
        legacy.WindowY = 1502;
        legacy.WindowLeft = null;
        legacy.WindowTop = null;
        Store.Save(legacy);
        string json = _file.Read();

        Assert.Contains("\"WindowX\": 980", json);
        Assert.DoesNotContain("WindowLeft", json);
        Assert.Equal(1502, Store.Load().WindowY);
    }

    [Fact]
    public void Profiles_OldFileBecomesThisPcSection_AndANewPcInheritsOnlyPersonalChoices()
    {
        var desktop = new ProfileIdentity("DESKTOP-SALA", "S-1-5-21-200", "joao");
        _file.Write("{ \"WindowLeft\": 653, \"WindowTop\": 1001, \"Layout\": \"CompactHorizontal\", \"Theme\": \"Dark\" }");

        AppSettings notebook = Store.Load();
        Assert.Equal(ViewLayout.CompactHorizontal, notebook.Layout);
        Assert.Equal(AppTheme.Dark, notebook.Theme);
        Assert.Equal(653, notebook.WindowLeft);

        notebook.WindowX = 980;
        notebook.WindowY = 1502;
        notebook.WindowLeft = null;
        notebook.WindowTop = null;
        notebook.Display = notebook.Display with { ShowFps = true, DiskSource = DiskSource.Specific, DiskNumber = 1, ShowGpu = false };
        notebook.Network = new NetworkSettings { PingHost = "1.1.1.1", Devices = new NetworkDeviceChoices([new(Guid.NewGuid(), false)]), DefaultDevice = new NetworkDeviceRef(Guid.NewGuid(), "Wi-Fi") };
        notebook.Appearance = new AppearanceSettings { CornerRadius = 8, BarRadius = 3 };
        notebook.LastSpeedTest = new SpeedTestResult(DateTimeOffset.Now, 1e8, 1e7, 20, 2);
        Assert.True(Store.Save(notebook));
        Assert.Contains("\"Profiles\"", _file.Read());
        Assert.Contains("\"Computer\": \"NOTEBOOK\"", _file.Read());

        AppSettings newPc = _file.For(desktop).Load();

        // Personal taste comes along.
        Assert.Equal(ViewLayout.CompactHorizontal, newPc.Layout);
        Assert.Equal(AppTheme.Dark, newPc.Theme);
        Assert.Equal(3, newPc.Appearance.BarRadius);
        Assert.False(newPc.Display.ShowGpu);
        Assert.Equal("1.1.1.1", newPc.Network.PingHost);

        // What belongs to the PC starts over.
        Assert.Null(newPc.WindowX);
        Assert.Equal(DiskSource.System, newPc.Display.DiskSource);
        Assert.Null(newPc.Display.DiskNumber);
        Assert.Equal(NetworkDeviceChoices.Empty, newPc.Network.Devices);
        Assert.Null(newPc.Network.DefaultDevice);
        Assert.Null(newPc.NetworkSession);
        Assert.Null(newPc.LastSpeedTest);
        Assert.False(newPc.Display.ShowFps);
    }

    [Fact]
    public void Profiles_TwoPcsShareTheFolderWithoutOverwritingEachOther()
    {
        var desktop = new ProfileIdentity("DESKTOP-SALA", "S-1-5-21-200", "joao");
        AppSettings notebook = Store.Load();
        notebook.WindowX = 980;
        Store.Save(notebook);

        SettingsStore other = _file.For(desktop);
        AppSettings otherSettings = other.Load();
        otherSettings.Theme = AppTheme.Light;
        other.Save(otherSettings);

        notebook.Layout = ViewLayout.Full;
        Store.Save(notebook);

        Assert.Equal(ViewLayout.Full, _file.For(TempSettingsFile.Profile).Load().Layout);
        Assert.Equal(980, _file.For(TempSettingsFile.Profile).Load().WindowX);
        Assert.Equal(AppTheme.Light, _file.For(desktop).Load().Theme);

        StoredProfile listed = Assert.Single(Store.ListOtherProfiles());
        Assert.Equal("DESKTOP-SALA", listed.Computer);
        Assert.Equal("joao", listed.UserName);
        Assert.NotNull(listed.LastUsed);
    }

    [Fact]
    public void Profiles_OtherSectionsKeepFieldsThisVersionDoesNotKnow_AndCanBeRemoved()
    {
        var desktop = new ProfileIdentity("DESKTOP-SALA", "S-1-5-21-200", "joao");
        AppSettings notebook = Store.Load();
        Store.Save(notebook);
        _file.For(desktop).Save(new AppSettings { Theme = AppTheme.Light });

        JsonNode document = JsonNode.Parse(_file.Read())!;
        document["Profiles"]!.AsArray().First(profile => (string?)profile!["Computer"] == "DESKTOP-SALA")!["Settings"]!["FutureVersionField"] = 42;
        _file.Write(document.ToJsonString());

        notebook.Layout = ViewLayout.CompactVertical;
        Store.Save(notebook);
        Assert.Contains("FutureVersionField", _file.Read());

        Assert.True(Store.RemoveOtherProfiles(notebook));
        Assert.Empty(Store.ListOtherProfiles());
        Assert.DoesNotContain("DESKTOP-SALA", _file.Read());
        Assert.Equal(ViewLayout.CompactVertical, _file.For(TempSettingsFile.Profile).Load().Layout);
        Assert.False(File.Exists(_file.FilePath + ".lock"));
        Assert.False(File.Exists(_file.FilePath + ".tmp"));
    }

    [Fact]
    public void Profiles_ComputerNameIgnoresCase_LikeWindows()
    {
        Store.Save(new AppSettings { Layout = ViewLayout.CompactVertical });
        SettingsStore lowercase = _file.For(new ProfileIdentity("notebook", "S-1-5-21-100", "cobia"));

        Assert.Equal(ViewLayout.CompactVertical, lowercase.Load().Layout);
        Assert.Empty(lowercase.ListOtherProfiles());
    }

    [Fact]
    public void NewComputer_DoesNotInheritTheGpuChoice_ButKeepsThePing()
    {
        var withGpu = new AppSettings { Display = new DisplaySettings { Gpu = new GpuChoice(0x8086, 0x5917, 0, "Intel(R) UHD Graphics 620") } };
        var withPing = new AppSettings { Network = new NetworkSettings { PingEnabled = true, PingHost = "1.1.1.1" } };

        Assert.Null(withGpu.ForNewComputer().Display.Gpu);
        Assert.True(withPing.ForNewComputer().Network.PingEnabled);
        Assert.Equal("1.1.1.1", withPing.ForNewComputer().Network.PingHost);
    }
}
