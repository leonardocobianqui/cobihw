using CobiHw.Core.Network;

namespace CobiHw.Tests.Core;

/// <summary>The public IP from Cloudflare's trace endpoint.</summary>
public sealed class PublicAddressTests
{
    [Fact]
    public void ReadsTheIpLineOfTheTrace()
    {
        Assert.Equal("200.1.2.3", PublicAddress.Parse("fl=123\nh=www.cloudflare.com\nip=200.1.2.3\nts=1\n"));
        Assert.Equal("2804:14c::1", PublicAddress.Parse("ip=2804:14c::1\r\n"));
        Assert.Null(PublicAddress.Parse("fl=1\n"));
    }
}
