using CobiHw.App.Settings;
using static CobiHw.Tests.TestData;

namespace CobiHw.Tests.App;

/// <summary>Appearance tab: summaries, scale synced from the widget and Restore defaults.</summary>
[Collection(AppState.Name)]
public sealed class SettingsAppearanceTabTests : LocalizedTest
{
    private static readonly AppearanceSettings Custom = new() { Scale = 150, CornerRadius = 8, BarRadius = 3, Transparency = 40, BorderMode = BorderMode.Always };
    private static readonly ColorSettings CustomColors = new() { Enabled = false, Cpu = new UsageThresholds(20, 30) };

    [Fact]
    public void Apply_IsOffWithoutPendingRestore_RestoreDefaultsIsOn()
    {
        using var settings = SettingsWindow();
        settings.SelectedTab = SettingsTab.Appearance;
        Assert.False(settings.ApplyCommand.CanExecute(null));
        Assert.True(settings.RestoreDefaultsCommand.CanExecute(null));

        settings.SelectedTab = SettingsTab.Display;
        Assert.True(settings.RestoreDefaultsCommand.CanExecute(null));
    }

    [Fact]
    public void ScaleSyncedFromTheWidget_IsNotPending_AndIsSentByAnyTab()
    {
        using var settings = SettingsWindow();
        Assert.Equal("100% · cantos 0 px · barras 0 px · sem borda", settings.ShapeSummary);
        Assert.False(settings.HasPendingChanges);

        settings.SyncScale(90);
        Assert.StartsWith("90%", settings.ShapeSummary);
        Assert.False(settings.HasPendingChanges);

        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.SelectedTab = SettingsTab.Display;
        settings.DetailedTooltip = false;
        settings.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.Equal(90, applied.Appearance.Scale);
        Assert.False(applied.Display.DetailedTooltip);
    }

    [Fact]
    public void RestoreDefaults_PreviewsTheDefaults_UntilApplied()
    {
        using var settings = SettingsWindow(Settings(colors: CustomColors, appearance: Custom));
        settings.SelectedTab = SettingsTab.Appearance;
        var previews = new List<AppearancePreview>();
        settings.AppearancePreviewed += (_, preview) => previews.Add(preview);
        SavedSettings? applied = null;
        int count = 0;
        settings.Applied += (_, saved) =>
        {
            applied = saved;
            count++;
        };
        Assert.False(settings.IsAppearancePending);
        Assert.Null(settings.PendingAppearance);
        Assert.Equal("150% · cantos 8 px · barras 3 px · borda 1 px (sempre)", settings.ShapeSummary);
        Assert.Equal("Desligadas", settings.UsageColorsSummary);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.True(settings.IsAppearancePending);
        Assert.Equal(new AppearancePreview(new AppearanceSettings(), new ColorSettings()), Assert.Single(previews));
        Assert.Equal("100% · cantos 0 px · barras 0 px · sem borda", settings.ShapeSummary);
        Assert.StartsWith("Ligadas · CPU 50/75", settings.UsageColorsSummary);
        Assert.Null(applied);
        Assert.True(settings.ApplyCommand.CanExecute(null));
        Assert.True(settings.HasPendingChanges);
        Assert.Equal("Aparência", settings.PendingTabNames);
        Assert.True(settings.CanApplyAll);

        // Resizing the widget meanwhile becomes part of the pending appearance.
        settings.SyncScale(120);
        Assert.True(settings.IsAppearancePending);
        Assert.Equal(120, settings.ShownAppearance.Appearance.Scale);
        Assert.Equal(0, settings.ShownAppearance.Appearance.CornerRadius);

        // Applying another tab keeps the saved appearance and the pending restore.
        settings.SelectedTab = SettingsTab.Display;
        settings.DetailedTooltip = false;
        settings.ApplyCommand.Execute(null);
        Assert.NotNull(applied);
        Assert.Equal(8, applied.Appearance.CornerRadius);
        Assert.Equal(120, applied.Appearance.Scale);
        Assert.True(settings.IsAppearancePending);

        settings.SelectedTab = SettingsTab.Appearance;
        settings.ApplyCommand.Execute(null);
        Assert.Equal(new AppearanceSettings { Scale = 120 }, applied.Appearance);
        Assert.Equal(new ColorSettings(), applied.Colors);
        Assert.False(settings.IsAppearancePending);
        Assert.Equal(2, count);
        Assert.False(settings.ApplyCommand.CanExecute(null));

        settings.SyncScale(100);
        settings.RestoreDefaultsCommand.Execute(null);
        Assert.False(settings.IsAppearancePending);
        Assert.False(settings.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void EditorApplyWhileRestoreIsPending_SavesWhatIsShown()
    {
        using var settings = SettingsWindow(Settings(colors: CustomColors, appearance: Custom));
        settings.SelectedTab = SettingsTab.Appearance;
        settings.RestoreDefaultsCommand.Execute(null);

        settings.SyncAppearance(new AppearanceSettings { Transparency = 10 }, new ColorSettings());

        Assert.False(settings.IsAppearancePending);
        Assert.Contains("10% transparente", settings.TextColorsSummary);
    }

    [Fact]
    public void ApplyAll_SavesTheRestoredAppearance()
    {
        using var settings = SettingsWindow(Settings(colors: CustomColors, appearance: Custom));
        settings.SelectedTab = SettingsTab.Appearance;
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.RestoreDefaultsCommand.Execute(null);

        settings.ApplyAll();

        Assert.NotNull(applied);
        Assert.Equal(new AppearanceSettings(), applied.Appearance);
        Assert.Equal(new ColorSettings(), applied.Colors);
        Assert.False(settings.HasPendingChanges);
    }
}
