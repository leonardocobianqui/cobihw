using CobiHw.App.Resources;
using CobiHw.App.Settings;
using CobiHw.Core;
using static CobiHw.Tests.TestData;

namespace CobiHw.Tests.App;

/// <summary>Display tab of the settings window: items, order, disk, GPU, FPS and extra values.</summary>
[Collection(AppState.Name)]
public sealed class SettingsDisplayTabTests : LocalizedTest
{
    private const ulong MiB = 1024UL * 1024;
    private const ulong GiB = 1024UL * MiB;

    private static readonly GpuActivity Integrated = new(new GpuId(0x8086, 0x5917, 0), "Intel(R) UHD Graphics 620", 1, true, 12, 128 * MiB, 128 * MiB, 1100 * MiB);
    private static readonly GpuActivity Dedicated = new(new GpuId(0x10DE, 0x2520, 0), "NVIDIA GeForce RTX 3060 Laptop GPU", 0, false, 40, 2355 * MiB, 6 * GiB, null);

    [Fact]
    public void DiskOptions_WindowsDiskFirst_WithoutRepeatingIt()
    {
        var settings = SettingsWindow();

        Assert.Equal(3, settings.DiskOptions.Count);
        Assert.Equal(DiskSource.System, settings.DiskOptions[0].Source);
        Assert.Equal("Disco do Windows — Disco 0 (C:)", settings.DiskOptions[0].Label);
        Assert.All(settings.DiskOptions.Skip(1), option => Assert.Equal(DiskSource.Specific, option.Source));
        Assert.Equal("Disco 1 (D: E:)", settings.DiskOptions[1].Label);
        Assert.Equal("Disco 2", settings.DiskOptions[2].Label);
    }

    [Fact]
    public void DiskOptions_WithASingleDisk_HaveOneOption()
    {
        var settings = SettingsWindow(disks: [new DiskActivity(0, "C:", 3, true)], monitors: []);

        DiskOption option = Assert.Single(settings.DiskOptions);
        Assert.Equal("Disco do Windows — Disco 0 (C:)", option.Label);
    }

    [Fact]
    public void DiskSource_HasOnlyWindowsAndSpecific() => Assert.Equal(new[] { "System", "Specific" }, Enum.GetNames<DiskSource>().ToList());

    [Fact]
    public void SavedSpecificDisk_IsSelected_AndHiddenItemsAreUnchecked()
    {
        var settings = SettingsWindow(Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 1, ShowGpu = false }));

        Assert.Equal(new DiskOption(DiskSource.Specific, 1, "Disco 1 (D: E:)"), settings.SelectedDisk);
        Assert.False(Row(settings, MetricItem.Gpu).IsVisible);
        Assert.True(Row(settings, MetricItem.Cpu).IsVisible);
    }

    [Fact]
    public void MissingSpecificDisk_IsShownAsNotFound() =>
        Assert.Equal(
            new DiskOption(DiskSource.Specific, 7, "Disco 7 (não encontrado)"),
            SettingsWindow(Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 7 })).SelectedDisk);

    [Fact]
    public void WithoutAKnownWindowsDisk_TheSimpleLabelIsSelected()
    {
        var settings = SettingsWindow(disks: [new DiskActivity(3, "E:", 1, false)], monitors: []);

        Assert.Equal("Disco do Windows", settings.DiskOptions[0].Label);
        Assert.Equal(DiskSource.System, settings.SelectedDisk?.Source);
    }

    [Fact]
    public void NothingChecked_IsAnError_UntilOneItemIsChecked()
    {
        var settings = SettingsWindow();
        settings.SelectedTab = SettingsTab.Display;

        foreach (MetricItem item in new[] { MetricItem.Cpu, MetricItem.Gpu, MetricItem.Memory, MetricItem.Disk, MetricItem.Network })
        {
            Row(settings, item).IsVisible = false;
        }

        Assert.True(settings.HasDisplayError);
        Assert.False(settings.ApplyCommand.CanExecute(null));

        Row(settings, MetricItem.Network).IsVisible = true;
        Assert.False(settings.HasDisplayError);
        Assert.True(settings.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Apply_SendsTheDisplaySettings_AndRestoreDefaultsChecksEverything()
    {
        var settings = SettingsWindow(Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 1 }));
        settings.SelectedTab = SettingsTab.Display;
        Row(settings, MetricItem.Cpu).IsVisible = false;
        settings.SelectedDisk = settings.DiskOptions[2];
        settings.DetailedTooltip = false;
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;

        settings.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.False(applied.Display.ShowCpu);
        Assert.True(applied.Display.ShowNetwork);
        Assert.Equal(DiskSource.Specific, applied.Display.DiskSource);
        Assert.Equal(2, applied.Display.DiskNumber);
        Assert.False(applied.Display.DetailedTooltip);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.All(settings.Metrics, row => Assert.True(row.IsVisible));
        Assert.Equal(DiskSource.System, settings.SelectedDisk?.Source);
        Assert.True(settings.DetailedTooltip);
    }

    [Fact]
    public void Apply_IsEnabledOnlyWhileTheTabDiffersFromWhatWasApplied()
    {
        var settings = SettingsWindow();
        settings.SelectedTab = SettingsTab.Display;
        int applied = 0;
        settings.Applied += (_, _) => applied++;
        Assert.False(settings.ApplyCommand.CanExecute(null));

        Row(settings, MetricItem.Gpu).IsVisible = false;
        Assert.True(settings.ApplyCommand.CanExecute(null));

        settings.ApplyCommand.Execute(null);
        Assert.Equal(1, applied);
        Assert.False(settings.ApplyCommand.CanExecute(null));

        Row(settings, MetricItem.Gpu).IsVisible = true;
        Assert.True(settings.ApplyCommand.CanExecute(null));

        Row(settings, MetricItem.Gpu).IsVisible = false;
        Assert.False(settings.ApplyCommand.CanExecute(null));

        settings.SelectedDisk = settings.DiskOptions[1];
        Assert.True(settings.ApplyCommand.CanExecute(null));

        settings.SelectedDisk = settings.DiskOptions[0];
        Assert.False(settings.ApplyCommand.CanExecute(null));
        Assert.False(settings.HasPendingChanges);
    }

    [Fact]
    public void RestoreDefaults_WhenAlreadyDefault_LeavesNothingToApply()
    {
        var settings = SettingsWindow();
        settings.RestoreDefaultsCommand.Execute(null);

        Assert.False(settings.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Order_OpensAsSaved_AndDraggingChangesIt()
    {
        var settings = SettingsWindow(Settings(new DisplaySettings { Order = new([MetricItem.Network, MetricItem.Cpu]), ShowDisk = false }));
        settings.SelectedTab = SettingsTab.Display;
        Assert.Equal(new[] { "Rede", "CPU", "GPU", "RAM", "Disco", "Bateria" }, settings.Metrics.Select(row => row.Name));
        Assert.False(Row(settings, MetricItem.Disk).IsVisible);
        Assert.False(settings.ApplyCommand.CanExecute(null));

        settings.MoveMetric(Row(settings, MetricItem.Disk), 0);
        Assert.Equal(MetricItem.Disk, settings.Metrics[0].Item);
        Assert.True(settings.ApplyCommand.CanExecute(null));

        settings.MoveMetric(Row(settings, MetricItem.Disk), 4);
        Assert.False(settings.ApplyCommand.CanExecute(null));

        settings.MoveMetric(Row(settings, MetricItem.Memory), 0);
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.Equal(new[] { MetricItem.Memory, MetricItem.Network, MetricItem.Cpu, MetricItem.Gpu, MetricItem.Disk, MetricItem.Battery }, applied.Display.Order.Items);
        Assert.False(applied.Display.ShowDisk);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.Equal(MetricOrder.Default.Items, settings.Metrics.Select(row => row.Item));
        Assert.All(settings.Metrics, row => Assert.True(row.IsVisible));
    }

    [Fact]
    public void Fps_CheckApplyRejectAndNotices()
    {
        using var settings = SettingsWindow();
        settings.SelectedTab = SettingsTab.Display;
        Assert.Equal(6, settings.Metrics.Count);
        Assert.DoesNotContain(settings.Metrics, row => row.Item == MetricItem.Fps);
        Assert.False(settings.ShowFps);
        Assert.False(settings.HasFpsNotice);
        Assert.Null(settings.FpsNotice);

        settings.ShowFps = true;
        Assert.True(settings.IsDisplayPending);
        Assert.True(settings.ApplyCommand.CanExecute(null));

        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.ApplyCommand.Execute(null);
        Assert.True(applied?.Display.ShowFps);
        Assert.False(settings.IsDisplayPending);

        // UAC declined: the box is unchecked without leaving anything pending.
        settings.RejectFps(nameof(Strings.Fps_Denied));
        Assert.False(settings.ShowFps);
        Assert.False(settings.IsDisplayPending);
        Assert.True(settings.HasFpsNotice);
        Assert.Equal("O Windows não concedeu a permissão de administrador. O FPS continua desligado.", settings.FpsNotice);

        SetLanguage("en-US");
        Assert.Equal("Windows did not grant administrator permission. FPS stays off.", settings.FpsNotice);
        SetLanguage(TestLanguage);

        settings.ShowFps = true;
        Assert.False(settings.HasFpsNotice);
        Assert.Null(settings.FpsNotice);

        settings.ShowFpsNotice(nameof(Strings.Fps_RestartPending));
        Assert.True(settings.ShowFps);
        Assert.Equal("O FPS começa a ser medido na próxima vez que o cobihw abrir.", settings.FpsNotice);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.False(settings.ShowFps);
    }

    [Fact]
    public void Fps_SavedOn_OpensChecked()
    {
        using var settings = SettingsWindow(Settings(new DisplaySettings { ShowFps = true }));

        Assert.True(settings.ShowFps);
        Assert.False(settings.IsDisplayPending);
    }

    [Fact]
    public void GpuOptions_AutomaticFirst_ThenWindowsOrder()
    {
        var settings = SettingsWindow(gpus: [Integrated, Dedicated]);
        settings.SelectedTab = SettingsTab.Display;

        Assert.Equal(
            new[] { "Automática — NVIDIA GeForce RTX 3060 Laptop GPU", "NVIDIA GeForce RTX 3060 Laptop GPU", "Intel(R) UHD Graphics 620" },
            settings.GpuOptions.Select(option => option.Label));
        Assert.Equal(settings.GpuOptions[0], settings.SelectedGpu);

        settings.SelectedGpu = settings.GpuOptions[2];
        Assert.True(settings.IsDisplayPending);

        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.ApplyCommand.Execute(null);
        Assert.Equal(new GpuChoice(0x8086, 0x5917, 0, "Intel(R) UHD Graphics 620"), applied?.Display.Gpu);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.Null(settings.SelectedGpu?.Choice);
    }

    [Fact]
    public void GpuOptions_SavedGpuThatIsGone_StaysSelectedAsNotFound()
    {
        var settings = SettingsWindow(Settings(new DisplaySettings { Gpu = new GpuChoice(0x1002, 0x73DF, 0, "AMD Radeon RX 6700 XT") }), gpus: [Integrated, Dedicated]);

        Assert.Equal("AMD Radeon RX 6700 XT (não encontrada)", settings.SelectedGpu?.Label);
        Assert.Equal(4, settings.GpuOptions.Count);
        Assert.False(settings.IsDisplayPending);
    }

    [Fact]
    public void GpuOptions_WithoutGpu_OnlyAutomatic()
    {
        GpuOption option = Assert.Single(SettingsWindow().GpuOptions);
        Assert.Equal("Automática", option.Label);
    }

    [Fact]
    public void GpuOptions_TwoIdenticalCards_AreNumbered()
    {
        GpuActivity second = Dedicated with { Id = Dedicated.Id with { Instance = 1 }, Rank = 1 };
        var settings = SettingsWindow(gpus: [Dedicated, second]);

        Assert.Equal("NVIDIA GeForce RTX 3060 Laptop GPU (1)", settings.GpuOptions[1].Label);
        Assert.Equal("NVIDIA GeForce RTX 3060 Laptop GPU (2)", settings.GpuOptions[2].Label);
    }

    [Fact]
    public void Battery_RowIsAvailableOnlyWithABattery()
    {
        var without = SettingsWindow();
        var with = SettingsWindow(hasBattery: true);

        Assert.False(Row(without, MetricItem.Battery).IsAvailable);
        Assert.True(Row(with, MetricItem.Battery).IsAvailable);
        Assert.True(Row(with, MetricItem.Battery).IsVisible);
    }

    [Fact]
    public void Battery_WithoutBattery_OnlyTheBatteryCheckedCountsAsNothing()
    {
        var settings = SettingsWindow();
        foreach (MetricRowViewModel row in settings.Metrics.Where(row => row.Item != MetricItem.Battery))
        {
            row.IsVisible = false;
        }

        Assert.True(settings.HasDisplayError);
    }

    [Fact]
    public void DiskSpeed_IsSaved()
    {
        var settings = SettingsWindow();
        settings.SelectedTab = SettingsTab.Display;
        settings.ShowDiskSpeed = true;
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;

        settings.ApplyCommand.Execute(null);

        Assert.True(applied?.Display.ShowDiskSpeed);
    }

    [Fact]
    public void GpuTemperature_IsSaved_AndRestoreDefaultsTurnsItOff()
    {
        var settings = SettingsWindow(gpus: [Dedicated]);
        settings.SelectedTab = SettingsTab.Display;
        settings.ShowGpuTemperature = true;
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;

        settings.ApplyCommand.Execute(null);
        Assert.True(applied?.Display.ShowGpuTemperature);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.False(settings.ShowGpuTemperature);
    }
}
