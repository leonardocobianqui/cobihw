using CobiHw.Core.Linux;

namespace CobiHw.Core.Tests.Linux;

/// <summary>Network interface ids derived from the name.</summary>
public sealed class InterfaceIdTests
{
    [Fact]
    public void SameName_SameId_AcrossRuns_AndAVersion5Uuid()
    {
        Guid wifi = InterfaceId.From("wlp2s0");

        // Fixed value: a change here would lose the devices users chose in the settings.
        Assert.Equal(Guid.Parse("a0a5f742-ac14-5905-a46b-eb3a0790ffa8"), wifi);
        Assert.Equal(5, wifi.Version);
        Assert.NotEqual(wifi, InterfaceId.From("enp3s0"));
    }
}
