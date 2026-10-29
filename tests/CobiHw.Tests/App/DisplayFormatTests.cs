using CobiHw.App;
using CobiHw.Core;
using CobiHw.Tests.Core;

namespace CobiHw.Tests.App;

/// <summary>Texts shown on the widget: units, three significant digits and languages.</summary>
[Collection(AppState.Name)]
public sealed class DisplayFormatTests : LocalizedTest
{
    private const double KB = 1024;
    private const double MB = 1024 * KB;
    private const double GB = 1024 * MB;

    [Fact]
    public void Percent_RoundsAndHasNoSpace()
    {
        Assert.Equal("13%", DisplayFormat.Percent(13.4));
        Assert.Equal("50%", DisplayFormat.Percent(49.5));
        Assert.Equal("—", DisplayFormat.Percent(null));

        SetLanguage("ru-RU");
        Assert.Equal("13%", DisplayFormat.Percent(13.4));
    }

    [Fact]
    public void Bandwidth_ThreeDigits_InTheAppLanguage()
    {
        Assert.Equal("245 Mbps", DisplayFormat.Bandwidth(245.3e6));
        Assert.Equal("1,20 Gbps", DisplayFormat.Bandwidth(1.2e9));

        SetLanguage("en-US");
        Assert.Equal("24.5 Mbps", DisplayFormat.Bandwidth(24.53e6));
        Assert.Equal("2.60 GHz", DisplayFormat.Gigahertz(2.6));
        Assert.Equal("1.50 KB", DisplayFormat.Size(1536));

        SetLanguage("es-ES");
        Assert.Equal("245 Mbps", DisplayFormat.Bandwidth(245.3e6));
        Assert.Equal("1,20 Gbps", DisplayFormat.Bandwidth(1.2e9));

        SetLanguage("ru-RU");
        Assert.Equal("245 Мбит/с", DisplayFormat.Bandwidth(245.3e6));
    }

    [Fact]
    public void BandwidthNumberAndUnit_SplitTheBandwidthText()
    {
        Assert.Equal(("245", "Mbps"), (DisplayFormat.BandwidthNumber(245.3e6), DisplayFormat.BandwidthUnit(245.3e6)));
        Assert.Equal(("1,20", "Gbps"), (DisplayFormat.BandwidthNumber(1.2e9), DisplayFormat.BandwidthUnit(1.2e9)));
        // 999.6 Mbps would round to "1000", so it's already Gbps, as in Bandwidth.
        Assert.Equal(("1,00", "Gbps"), (DisplayFormat.BandwidthNumber(999.6e6), DisplayFormat.BandwidthUnit(999.6e6)));

        SetLanguage("ru-RU");
        Assert.Equal("Мбит/с", DisplayFormat.BandwidthUnit(245.3e6));
    }

    [Fact]
    public void DiskRates_ThreeSignificantDigits()
    {
        // Two decimals below 10, one below 100, none from 100; zero stays "0".
        Assert.Equal("L 1,23 MB/s · E 45,7 MB/s", DisplayFormat.DiskRates(1.234 * MB, 45.67 * MB));
        Assert.Equal("L 512 KB/s · E 0 KB/s", DisplayFormat.DiskRates(512 * KB, 0));
        // 9.996 would print "10.00" and 99.96 "100.0", so they already drop a decimal.
        Assert.Equal("L 10,0 MB/s · E 100 MB/s", DisplayFormat.DiskRates(9.996 * MB, 99.96 * MB));
    }

    [Fact]
    public void DiskRates_EachValuePicksItsOwnUnit()
    {
        Assert.Equal("L 2,00 MB/s · E 300 KB/s", DisplayFormat.DiskRates(2 * MB, 300 * KB));
        Assert.Equal("L 999 KB/s · E 0 KB/s", DisplayFormat.DiskRates(999 * KB, 0));
        // 999.6 KB/s would print "1000", so it is already shown in MB/s.
        Assert.Equal("L 0,98 MB/s · E 0 KB/s", DisplayFormat.DiskRates(999.6 * KB, 0));
        Assert.Equal("L 999 MB/s · E 0 KB/s", DisplayFormat.DiskRates(999 * MB, 0));
        Assert.Equal("L 0,98 GB/s · E 0 KB/s", DisplayFormat.DiskRates(999.6 * MB, 0));
        Assert.Equal("L 3,20 GB/s · E 1,10 GB/s", DisplayFormat.DiskRates(3.2 * GB, 1.1 * GB));
    }

    [Fact]
    public void DiskRates_MissingReading_IsADash()
    {
        Assert.Equal("—", DisplayFormat.DiskRates(null, 5));
        Assert.Equal("—", DisplayFormat.DiskRates(5, null));
    }

    [Fact]
    public void DiskRates_InOtherLanguages()
    {
        SetLanguage("en-US");
        Assert.Equal("R 12.3 MB/s · W 4.10 MB/s", DisplayFormat.DiskRates(12.3 * MB, 4.1 * MB));

        SetLanguage("ru-RU");
        Assert.Equal("Ч 3,20 ГБ/с · З 0 КБ/с", DisplayFormat.DiskRates(3.2 * GB, 0));
    }

    [Fact]
    public void WidestTexts_AreTheWidestThreeDigitNumbers()
    {
        Assert.Equal("L 99,9 MB/s · E 99,9 MB/s", DisplayFormat.DiskRatesWidest);
        Assert.Equal("99,9 Mbps", DisplayFormat.NetworkRateWidest);
    }

    [Theory]
    [InlineData(0.05, "0,05 Mbps")]
    [InlineData(245.3, "245 Mbps")]
    [InlineData(999, "999 Mbps")]
    [InlineData(999.6, "1,00 Gbps")]
    [InlineData(2500, "2,50 Gbps")]
    [InlineData(0, "0 Mbps")]
    public void NetworkRate_ThreeDigits_SwitchesToGbps(double megabits, string expected) =>
        Assert.Equal(expected, DisplayFormat.NetworkRate(megabits * 1_000_000 / 8));

    [Fact]
    public void NetworkRate_InRussian()
    {
        SetLanguage("ru-RU");
        Assert.Equal("2,50 Гбит/с", DisplayFormat.NetworkRate(2_500_000_000 / 8.0));
    }

    [Fact]
    public void NetworkRate_MissingReading_IsADash() => Assert.Equal("—", DisplayFormat.NetworkRate(null));

    [Theory]
    [InlineData(512UL, "512 B")]
    [InlineData(2_276_000_000UL, "2,12 GB")]
    [InlineData(41_890_000UL, "39,9 MB")]
    [InlineData(1000UL * 1024, "0,98 MB")] // 1000 KB already becomes MB.
    public void Size_ThreeDigits_WholeBytes(ulong bytes, string expected) => Assert.Equal(expected, DisplayFormat.Size(bytes));

    [Fact]
    public void Megabits_ForTheTray_SameThreeDigitRule()
    {
        Assert.Equal("12,3", DisplayFormat.Megabits(12.34 * 1_000_000 / 8));
        Assert.Equal("0", DisplayFormat.Megabits(0));
    }

    [Fact]
    public void Temperature_WholeDegreesLikeTaskManager()
    {
        Assert.Equal("47 °C", DisplayFormat.Temperature(46.9));
        Assert.Equal("46 °C", DisplayFormat.Temperature(46.4));
        Assert.Equal("—", DisplayFormat.Temperature(null));
    }

    [Fact]
    public void GpuMemory_IntegratedShowsUsage_DedicatedShowsUsedOfTotal()
    {
        Assert.Equal("1,2 GB", DisplayFormat.GpuMemory(GpuTests.Intel));
        Assert.Equal("2,3 / 6,0 GB", DisplayFormat.GpuMemory(GpuTests.Nvidia));
        Assert.Equal("—", DisplayFormat.GpuMemory(null));
    }

    [Fact]
    public void GpuMemoryDetail_SplitsReservedAndSharedOnIntegratedGpus()
    {
        Assert.Equal("Dedicada 0,1 / 0,1 GB · Compartilhada 1,1 GB", DisplayFormat.GpuMemoryDetail(GpuTests.Intel));
        Assert.Equal("Compartilhada 1,1 GB", DisplayFormat.GpuMemoryDetail(GpuTests.Intel with { DedicatedTotalBytes = 0, DedicatedUsedBytes = null }));
        Assert.Equal("VRAM 2,3 / 6,0 GB", DisplayFormat.GpuMemoryDetail(GpuTests.Nvidia));
        Assert.Equal("—", DisplayFormat.GpuMemoryDetail(null));
    }

    [Fact]
    public void BatteryState_And_BatteryPercent()
    {
        Assert.Equal("Carregando", DisplayFormat.BatteryState(new BatterySnapshot(85, true, true, null)));
        Assert.Equal("Na tomada", DisplayFormat.BatteryState(new BatterySnapshot(100, false, true, null)));
        Assert.Equal("Restam 2 h 15 min", DisplayFormat.BatteryState(new BatterySnapshot(40, false, false, TimeSpan.FromMinutes(135))));
        Assert.Equal("Na bateria", DisplayFormat.BatteryState(new BatterySnapshot(40, false, false, null)));
        Assert.Equal("—", DisplayFormat.BatteryState(null));
        Assert.Equal("85%", DisplayFormat.BatteryPercent(new BatterySnapshot(85, false, true, null)));
        Assert.Equal("—", DisplayFormat.BatteryPercent(new BatterySnapshot(null, false, true, null)));
        Assert.Equal("—", DisplayFormat.BatteryPercent(null));
    }
}
