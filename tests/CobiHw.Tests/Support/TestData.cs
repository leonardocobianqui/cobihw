using CobiHw.App;
using CobiHw.App.Settings;
using CobiHw.Core;
using CobiHw.Core.Network;
using CobiHw.Tests.Fakes;

namespace CobiHw.Tests;

/// <summary>Builders for the settings, readings and view models shared by many tests.</summary>
internal static class TestData
{
    /// <summary>Three disks: the Windows disk (0), a busy one with two letters (1) and one without letters (2).</summary>
    public static IReadOnlyList<DiskActivity> Disks { get; } =
    [
        new(0, "C:", 12, true),
        new(1, "D: E:", 80, false),
        new(2, "", 5, false),
    ];

    /// <summary>Two monitors; the first is the main one.</summary>
    public static IReadOnlyList<DisplayMonitor> Monitors { get; } =
    [
        new(@"\\.\DISPLAY1", 1, 1920, 1080, true),
        new(@"\\.\DISPLAY2", 2, 2560, 1440, false),
    ];

    /// <summary>A typical reading: CPU at 13%, one NVIDIA GPU at 9%, RAM at 33%, the three <see cref="Disks"/>.</summary>
    public static HardwareSnapshot Snapshot { get; } = new(
        new CpuSnapshot("Intel(R) Xeon(R) CPU E5-2697A v4 @ 2.60GHz", 13, 2.6),
        new MemorySnapshot(10, 30),
        new GpuSnapshot([new GpuActivity(new GpuId(0x10DE, 0x2507, 0), "NVIDIA GeForce RTX 3050", 0, false, 9, 1, 2, null)]),
        new DiskSnapshot(Disks),
        new NetworkSnapshot(1, 2, 3, 4, []));

    /// <summary>Saved settings with defaults for every part not given.</summary>
    public static SavedSettings Settings(
        DisplaySettings? display = null,
        ColorSettings? colors = null,
        NetworkSettings? network = null,
        AppearanceSettings? appearance = null,
        string language = "auto",
        GeneralSettings? general = null) =>
        new(display ?? new(), colors ?? new(), network ?? new(), appearance ?? new(), (general ?? new()) with { Language = language });

    /// <summary>Speed test against the simulated Cloudflare server.</summary>
    public static SpeedTestViewModel SpeedTest(SpeedTestResult? lastResult = null) =>
        new(FakeCloudflare.Client(new FakeCloudflare()), lastResult);

    /// <summary>Widget state built from the given settings.</summary>
    public static MainViewModel Widget(SavedSettings? settings = null) => new(settings ?? Settings(), SpeedTest());

    /// <summary>Settings window state, with the shared disks and monitors unless others are given.</summary>
    public static SettingsViewModel SettingsWindow(
        SavedSettings? settings = null,
        IReadOnlyList<DiskActivity>? disks = null,
        IReadOnlyList<GpuActivity>? gpus = null,
        bool hasBattery = false,
        IReadOnlyList<DisplayMonitor>? monitors = null,
        IReadOnlyList<NetworkDeviceInfo>? devices = null) =>
        new(settings ?? Settings(), disks ?? Disks, gpus ?? [], hasBattery, monitors ?? Monitors, devices ?? [], SpeedTest());

    /// <summary>Row of an item in the Display tab.</summary>
    public static MetricRowViewModel Row(SettingsViewModel settings, MetricItem item) => settings.Metrics.First(row => row.Item == item);
}
