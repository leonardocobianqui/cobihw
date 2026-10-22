using CobiHw.App;
using CobiHw.App.Settings;
using static CobiHw.Tests.TestData;

namespace CobiHw.Tests.App;

/// <summary>General tab, pending changes per tab and Apply all.</summary>
[Collection(AppState.Name)]
public sealed class SettingsGeneralTabTests : LocalizedTest
{
    [Fact]
    public void OpensOnTheGeneralTab_WithNothingPending()
    {
        using var settings = SettingsWindow(Settings(general: new GeneralSettings { Monitor = @"\\.\DISPLAY2", SnapToEdges = true }));

        Assert.Equal(SettingsTab.General, settings.SelectedTab);
        Assert.False(settings.HasPendingChanges);
        Assert.Equal("", settings.PendingTabNames);
        Assert.False(settings.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Monitors_AndModes_AreNamed()
    {
        using var settings = SettingsWindow(Settings(general: new GeneralSettings { Monitor = @"\\.\DISPLAY2" }));

        Assert.Equal(2, settings.MonitorOptions.Count);
        Assert.Equal("Monitor 1 — 1920 × 1080 (principal)", settings.MonitorOptions[0].Label);
        Assert.Equal("Monitor 2 — 2560 × 1440", settings.MonitorOptions[1].Label);
        Assert.Equal(@"\\.\DISPLAY2", settings.Monitor);
        Assert.True(settings.HasSeveralMonitors);
        Assert.Equal(2, settings.ModeOptions.Count);
        Assert.Equal("Na área de trabalho", settings.ModeOptions[1].Label);
    }

    [Fact]
    public void PendingChanges_AreTrackedPerTab()
    {
        using var settings = SettingsWindow();
        settings.DetailedTooltip = false;

        Assert.True(settings.IsDisplayPending);
        Assert.False(settings.IsGeneralPending);
        Assert.False(settings.IsNetworkPending);
        Assert.Equal("Exibição", settings.PendingTabNames);
    }

    [Fact]
    public void ApplyAll_SendsEveryTab_AndTheSyncedAppearance()
    {
        using var settings = SettingsWindow();
        SavedSettings? applied = null;
        int count = 0;
        settings.Applied += (_, saved) =>
        {
            applied = saved;
            count++;
        };
        settings.DetailedTooltip = false;

        // Appearance applied from an editor window updates the summaries without leaving anything pending.
        settings.SyncAppearance(new AppearanceSettings { Transparency = 30 }, new ColorSettings { Cpu = new UsageThresholds(55, 75) });
        Assert.Contains("30% transparente", settings.TextColorsSummary);
        Assert.Contains("CPU 55/75", settings.UsageColorsSummary);
        Assert.False(settings.IsGeneralPending);

        Assert.False(settings.ApplyCommand.CanExecute(null));
        Assert.Equal(0, count);

        settings.SelectedTab = SettingsTab.Display;
        Assert.True(settings.ApplyCommand.CanExecute(null));

        settings.SelectedTab = SettingsTab.Network;
        settings.PingEnabled = true;
        settings.PingHostText = "bad host!";
        Assert.False(settings.ApplyCommand.CanExecute(null));
        Assert.False(settings.CanApplyAll);

        settings.SelectedTab = SettingsTab.Display;
        Assert.True(settings.ApplyCommand.CanExecute(null));

        settings.PingHostText = "1.1.1.1";
        settings.SelectedTab = SettingsTab.General;
        settings.ApplyAll();

        Assert.NotNull(applied);
        Assert.Equal(30, applied.Appearance.Transparency);
        Assert.Equal(55, applied.Colors.Cpu.Warning);
        Assert.Equal("1.1.1.1", applied.Network.PingHost);
        Assert.False(applied.Display.DetailedTooltip);
        Assert.False(settings.HasPendingChanges);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Apply_SendsTheGeneralTab()
    {
        using var settings = SettingsWindow();
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.Mode = WidgetMode.Desktop;
        settings.SnapToEdges = false;
        settings.PositionLocked = true;
        settings.StartWithWindows = true;
        settings.Monitor = @"\\.\DISPLAY1";
        settings.Language = "es-ES";

        settings.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.Equal("es-ES", applied.General.Language);
        Assert.Equal(WidgetMode.Desktop, applied.General.Mode);
        Assert.False(applied.General.SnapToEdges);
        Assert.True(applied.General.PositionLocked);
        Assert.True(applied.General.StartWithWindows);
        Assert.Equal(@"\\.\DISPLAY1", applied.General.Monitor);
    }

    [Fact]
    public void TrayMenuChanges_UpdateOnlyWhatChanged()
    {
        // Desktop is saved; switching to Always on top leaves that change pending.
        using var settings = SettingsWindow(Settings(general: new GeneralSettings { Mode = WidgetMode.Desktop }));
        settings.Mode = WidgetMode.AlwaysOnTop;

        settings.SyncGeneral(new GeneralSettings { Mode = WidgetMode.Desktop, SnapToEdges = true, PositionLocked = true, StartWithWindows = true, Monitor = @"\\.\DISPLAY2" });

        Assert.True(settings.SnapToEdges);
        Assert.Equal(@"\\.\DISPLAY2", settings.Monitor);
        Assert.Equal(WidgetMode.AlwaysOnTop, settings.Mode);
        Assert.True(settings.IsGeneralPending);
    }

    [Fact]
    public void RestoreDefaults_TouchesOnlyTheGeneralTab()
    {
        using var settings = SettingsWindow(Settings(general: new GeneralSettings { Mode = WidgetMode.Desktop, SnapToEdges = false, PositionLocked = true, Monitor = @"\\.\DISPLAY2" }, language: "es-ES"));

        settings.RestoreDefaultsCommand.Execute(null);

        Assert.Equal("auto", settings.Language);
        Assert.Equal(WidgetMode.AlwaysOnTop, settings.Mode);
        Assert.True(settings.SnapToEdges);
        Assert.False(settings.PositionLocked);
        Assert.False(settings.StartWithWindows);
        Assert.Equal(@"\\.\DISPLAY1", settings.Monitor);
        Assert.True(settings.IsGeneralPending);
        Assert.Equal("Geral", settings.PendingTabNames);

        SetLanguage("en-US");
        Assert.Equal("Monitor 1 — 1920 × 1080 (main)", settings.MonitorOptions[0].Label);
        Assert.Equal(@"\\.\DISPLAY1", settings.Monitor);
        Assert.Equal(WidgetMode.AlwaysOnTop, settings.Mode);
    }

    [Fact]
    public void Monitors_WithOneMonitorOrAnUnknownOne_UseTheMainMonitor()
    {
        using var one = SettingsWindow(monitors: [new(@"\\.\DISPLAY1", 1, 1920, 1080, true)]);
        Assert.False(one.HasSeveralMonitors);
        Assert.Equal(@"\\.\DISPLAY1", one.Monitor);
        Assert.False(one.HasPendingChanges);

        using var gone = SettingsWindow(Settings(general: new GeneralSettings { Monitor = @"\\.\DISPLAY9" }));
        Assert.Equal(@"\\.\DISPLAY1", gone.Monitor);
        Assert.False(gone.HasPendingChanges);
    }

    [Fact]
    public void Theme_LoadedChangedAndRestored()
    {
        using var settings = SettingsWindow(Settings(general: new GeneralSettings { Theme = AppTheme.Dark }));
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(3, settings.ThemeOptions.Count);
        Assert.Equal("Automático (igual ao Windows)", settings.ThemeOptions[0].Label);
        Assert.False(settings.HasPendingChanges);

        settings.Theme = AppTheme.Light;
        Assert.True(settings.IsGeneralPending);
        Assert.False(settings.IsDisplayPending);
        Assert.True(settings.ApplyCommand.CanExecute(null));

        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.ApplyCommand.Execute(null);
        Assert.NotNull(applied);
        Assert.Equal(AppTheme.Light, applied.General.Theme);
        Assert.False(settings.HasPendingChanges);

        // The tray menu has no theme option, so syncing from it leaves the theme alone.
        settings.SyncGeneral(applied.General with { Theme = AppTheme.Dark, SnapToEdges = !applied.General.SnapToEdges });
        Assert.Equal(AppTheme.Light, settings.Theme);
        Assert.False(settings.IsGeneralPending);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.Equal(AppTheme.System, settings.Theme);

        SetLanguage("en-US");
        Assert.Equal("Dark", settings.ThemeOptions[2].Label);
        Assert.Equal(AppTheme.System, settings.Theme);
    }

    [Fact]
    public void Theme_InvalidInTheFile_IsSystem()
    {
        using var settings = SettingsWindow(Settings(general: new GeneralSettings { Theme = (AppTheme)9 }));
        Assert.Equal(AppTheme.System, settings.Theme);
    }

    [Fact]
    public void Language_ChangeIsAppliedAndRestored()
    {
        using var settings = SettingsWindow(Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 1 }));
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;

        settings.Language = "ru-RU";
        Assert.True(settings.ApplyCommand.CanExecute(null));

        settings.ApplyCommand.Execute(null);
        Assert.Equal("ru-RU", applied?.General.Language);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.Equal("auto", settings.Language);
    }

    [Fact]
    public void Language_InvalidInTheFile_IsAutomatic()
    {
        using var settings = SettingsWindow(Settings(language: "klingon"));
        Assert.Equal("auto", settings.Language);
    }

    [Fact]
    public void ClickThrough_DefaultsApplySyncAndRestore()
    {
        using var settings = SettingsWindow();
        Assert.False(settings.ClickThrough);
        Assert.Equal(InteractionKey.Ctrl, settings.InteractionKey);
        Assert.Equal(6, settings.KeyOptions.Count);
        Assert.Equal("Nenhuma (só pelo menu do ícone)", settings.KeyOptions[0].Label);
        Assert.Equal("Ctrl + Shift", settings.KeyOptions[4].Label);
        Assert.False(settings.ApplyCommand.CanExecute(null));

        settings.ClickThrough = true;
        settings.InteractionKey = InteractionKey.CtrlShift;
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.ApplyCommand.Execute(null);
        Assert.NotNull(applied);
        Assert.True(applied.General.ClickThrough);
        Assert.Equal(InteractionKey.CtrlShift, applied.General.InteractionKey);

        // Turned off from the tray menu: the window follows without anything pending.
        settings.SyncGeneral(applied.General with { ClickThrough = false });
        Assert.False(settings.ClickThrough);
        Assert.False(settings.ApplyCommand.CanExecute(null));

        SetLanguage("en-US");
        Assert.Equal("None (tray icon menu only)", settings.KeyOptions[0].Label);
        Assert.Equal(InteractionKey.CtrlShift, settings.InteractionKey);
        Assert.False(settings.ApplyCommand.CanExecute(null));
        SetLanguage(TestLanguage);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.False(settings.ClickThrough);
        Assert.Equal(InteractionKey.Ctrl, settings.InteractionKey);
    }

    [Fact]
    public void ClickThrough_InvalidKeyInTheFile_IsCtrl()
    {
        using var settings = SettingsWindow(Settings(general: new GeneralSettings { InteractionKey = (InteractionKey)77 }));
        Assert.Equal(InteractionKey.Ctrl, settings.InteractionKey);
    }
}
