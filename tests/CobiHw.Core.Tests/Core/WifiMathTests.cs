using CobiHw.Core.Network;

namespace CobiHw.Core.Tests.Core;

/// <summary>Band and channel from the Wi-Fi center frequency.</summary>
public sealed class WifiMathTests
{
    [Theory]
    [InlineData(2437.0, 2.4, 6)]
    [InlineData(2484.0, 2.4, 14)]
    [InlineData(2412.0, 2.4, 1)]
    [InlineData(5180.0, 5.0, 36)]
    [InlineData(5745.0, 5.0, 149)]
    [InlineData(5955.0, 6.0, 1)]
    [InlineData(5935.0, 6.0, 2)]
    [InlineData(6115.0, 6.0, 33)]
    public void BandAndChannelFromFrequency(double megahertz, double band, int channel)
    {
        Assert.Equal(band, WifiMath.BandGhz(megahertz));
        Assert.Equal(channel, WifiMath.Channel(megahertz));
    }
}
