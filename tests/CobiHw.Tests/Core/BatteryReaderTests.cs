using CobiHw.Core;
using CobiHw.Core.Interop;
using CobiHw.Core.Readers;

namespace CobiHw.Tests.Core;

/// <summary>Battery state from the Windows power status.</summary>
[Collection(AppState.Name)]
public sealed class BatteryReaderTests
{
    private const uint Unknown = 0xFFFFFFFF;

    private static BatterySnapshot? Describe(byte ac, byte flag, byte percent, uint secondsLeft) =>
        BatteryReader.Describe(new Kernel32.SystemPowerStatus { ACLineStatus = ac, BatteryFlag = flag, BatteryLifePercent = percent, BatteryLifeTime = secondsLeft });

    [Fact]
    public void Desktop_HasNoBattery() => Assert.Null(Describe(1, 128, 255, Unknown));

    [Fact]
    public void UnknownState_HasNoBattery() => Assert.Null(Describe(255, 255, 255, Unknown));

    [Fact]
    public void ChargingOnPower() => Assert.Equal(new BatterySnapshot(85, true, true, null), Describe(1, 8 | 1, 85, Unknown));

    [Fact]
    public void OnBattery_WithTimeLeft() =>
        Assert.Equal(new BatterySnapshot(40, false, false, TimeSpan.FromMinutes(135)), Describe(0, 2, 40, 8100));

    [Fact]
    public void UnknownPercent_IsNull_AndAbove100IsCapped()
    {
        Assert.Null(Describe(0, 0, 255, Unknown)?.Percent);
        Assert.Equal(100, Describe(0, 0, 101, Unknown)?.Percent);
    }
}
