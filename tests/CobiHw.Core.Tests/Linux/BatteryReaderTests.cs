using CobiHw.Core.Linux.Readers;
using CobiHw.Core.Tests.Support;

namespace CobiHw.Core.Tests.Linux;

/// <summary>Battery from /sys/class/power_supply.</summary>
public sealed class BatteryReaderTests
{
    private static IReadOnlyDictionary<string, string> Supply(string uevent) => BatteryReader.ParseUevent(uevent);

    [Fact]
    public void Notebook_FullAndPluggedIn_HasNoTimeLeft()
    {
        using FakeSystem system = Notebook.Build();

        BatterySnapshot battery = BatteryReader.Read(system.Files)!;

        Assert.Equal(100, battery.Percent);
        Assert.False(battery.IsCharging);
        Assert.True(battery.OnPower);
        Assert.Null(battery.Remaining);
    }

    [Fact]
    public void Discharging_TimeLeftFromChargeAndCurrent_EvenWhenTheCurrentIsNegative()
    {
        string discharging = Notebook.BatteryFull
            .Replace("STATUS=Full", "STATUS=Discharging")
            .Replace("CURRENT_NOW=0", "CURRENT_NOW=-1736500")
            .Replace("CHARGE_NOW=3473000", "CHARGE_NOW=1736500")
            .Replace("CAPACITY=100", "CAPACITY=50");

        BatterySnapshot battery = BatteryReader.Describe([Supply(discharging), Supply(Notebook.Charger.Replace("ONLINE=1", "ONLINE=0"))])!;

        Assert.Equal(50, battery.Percent);
        Assert.False(battery.OnPower);
        Assert.Equal(TimeSpan.FromHours(1), battery.Remaining);
    }

    [Fact]
    public void Discharging_TimeLeftFromEnergyAndPower_OrFromTheDriverWhenItHasIt()
    {
        string energy = "POWER_SUPPLY_TYPE=Battery\nPOWER_SUPPLY_STATUS=Discharging\nPOWER_SUPPLY_ENERGY_NOW=30000000\nPOWER_SUPPLY_ENERGY_FULL=60000000\nPOWER_SUPPLY_POWER_NOW=10000000\nPOWER_SUPPLY_CAPACITY=50\n";

        Assert.Equal(TimeSpan.FromHours(3), BatteryReader.Describe([Supply(energy)])!.Remaining);
        Assert.Equal(TimeSpan.FromSeconds(5400), BatteryReader.Describe([Supply(energy + "POWER_SUPPLY_TIME_TO_EMPTY_NOW=5400\n")])!.Remaining);
    }

    [Fact]
    public void TwoBatteries_WeighByTheirSize()
    {
        string internalBattery = "POWER_SUPPLY_TYPE=Battery\nPOWER_SUPPLY_STATUS=Charging\nPOWER_SUPPLY_ENERGY_NOW=20000000\nPOWER_SUPPLY_ENERGY_FULL=20000000\nPOWER_SUPPLY_CAPACITY=100\n";
        string externalBattery = "POWER_SUPPLY_TYPE=Battery\nPOWER_SUPPLY_STATUS=Charging\nPOWER_SUPPLY_ENERGY_NOW=10000000\nPOWER_SUPPLY_ENERGY_FULL=60000000\nPOWER_SUPPLY_CAPACITY=17\n";

        BatterySnapshot battery = BatteryReader.Describe([Supply(internalBattery), Supply(externalBattery), Supply(Notebook.Charger)])!;

        Assert.Equal(38, battery.Percent);
        Assert.True(battery.IsCharging);
        Assert.Null(battery.Remaining);
    }

    [Fact]
    public void MouseBatteries_AndDesktopsWithoutABattery_HaveNoSystemBattery()
    {
        string mouse = "POWER_SUPPLY_TYPE=Battery\nPOWER_SUPPLY_SCOPE=Device\nPOWER_SUPPLY_CAPACITY=80\nPOWER_SUPPLY_STATUS=Discharging\n";

        Assert.Null(BatteryReader.Describe([Supply(mouse), Supply(Notebook.Charger)]));
        Assert.Null(BatteryReader.Describe([]));
        Assert.Null(BatteryReader.Describe([Supply(Notebook.BatteryFull.Replace("PRESENT=1", "PRESENT=0"))]));
    }

    [Fact]
    public void WithoutAChargerToAsk_TheStatusTellsWhetherItIsPluggedIn()
    {
        string charging = "POWER_SUPPLY_TYPE=Battery\nPOWER_SUPPLY_STATUS=Charging\nPOWER_SUPPLY_CAPACITY=70\n";
        string discharging = charging.Replace("Charging", "Discharging");

        Assert.True(BatteryReader.Describe([Supply(charging)])!.OnPower);
        Assert.False(BatteryReader.Describe([Supply(discharging)])!.OnPower);
    }
}
