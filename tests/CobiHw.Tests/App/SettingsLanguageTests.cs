using CobiHw.App;
using CobiHw.App.Settings;
using static CobiHw.Tests.TestData;

namespace CobiHw.Tests.App;

/// <summary>Settings window texts when the app language changes while it's open.</summary>
[Collection(AppState.Name)]
public sealed class SettingsLanguageTests : LocalizedTest
{
    private static SettingsViewModel OpenWithDiskOne() =>
        SettingsWindow(Settings(new DisplaySettings { DiskSource = DiskSource.Specific, DiskNumber = 1 }));

    [Fact]
    public void English_TextsChange_SelectionStays_NothingPending()
    {
        using SettingsViewModel settings = OpenWithDiskOne();
        Assert.Equal(new[] { "CPU", "GPU", "RAM", "Disco", "Rede", "Bateria" }, settings.Metrics.Select(row => row.Name));

        SetLanguage("en-US");

        Assert.Equal(new[] { "CPU", "GPU", "RAM", "Disk", "Network", "Battery" }, settings.Metrics.Select(row => row.Name));
        Assert.Equal("Disk 1 (D: E:)", settings.SelectedDisk?.Label);
        Assert.Equal("Windows disk — Disk 0 (C:)", settings.DiskOptions[0].Label);
        Assert.Equal("Automatic (Windows language)", settings.LanguageOptions[0].Name);
        Assert.False(settings.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void English_AppearanceSummaries()
    {
        using SettingsViewModel settings = OpenWithDiskOne();

        SetLanguage("en-US");

        Assert.Equal("100% · corners 0 px · bars 0 px · no border", settings.ShapeSummary);
        Assert.Equal("Background #1E1F22, 0% transparent · text #E6E6E6, normal", settings.TextColorsSummary);
        Assert.Equal("On · CPU 50/75 · GPU 50/75 · RAM 50/75 · Disk 50/75", settings.UsageColorsSummary);
    }

    [Fact]
    public void SpanishAndRussian_ItemNamesAndSummary()
    {
        using SettingsViewModel settings = OpenWithDiskOne();

        SetLanguage("es-ES");
        Assert.Equal("Red", settings.Metrics[4].Name);

        SetLanguage("ru-RU");
        Assert.Equal("ЦП", settings.Metrics[0].Name);
        Assert.Equal("Включены · ЦП 50/75 · ГП 50/75 · ОЗУ 50/75 · Диск 50/75", settings.UsageColorsSummary);

        SetLanguage("pt-BR");
        Assert.StartsWith("Ligadas · CPU 50/75", settings.UsageColorsSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterDispose_TheWindowStopsListening()
    {
        SettingsViewModel settings = OpenWithDiskOne();
        settings.Dispose();

        SetLanguage("en-US");

        Assert.Equal("Automático (idioma do Windows)", settings.LanguageOptions[0].Name);
    }
}
