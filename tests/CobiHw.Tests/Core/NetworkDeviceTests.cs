using System.Net;
using System.Runtime.InteropServices;
using CobiHw.Core.Interop;
using CobiHw.Core.Network;

namespace CobiHw.Tests.Core;

/// <summary>Network device type, visibility, address filters, Wi-Fi math and the WLAN structures.</summary>
[Collection(AppState.Name)]
public sealed class NetworkDeviceTests
{
    [Theory]
    [InlineData(71u, 9u, true, NetworkDeviceKind.WiFi)]
    [InlineData(6u, 14u, true, NetworkDeviceKind.Ethernet)]
    [InlineData(6u, 10u, true, NetworkDeviceKind.Bluetooth)] // Bluetooth PAN: Ethernet type on a Bluetooth medium.
    [InlineData(53u, 0u, false, NetworkDeviceKind.Virtual)] // Tailscale, WireGuard.
    [InlineData(6u, 0u, false, NetworkDeviceKind.Virtual)] // VPN TAP: Ethernet that isn't physical.
    [InlineData(131u, 0u, false, NetworkDeviceKind.Tunnel)] // Teredo, ISATAP.
    [InlineData(24u, 0u, false, NetworkDeviceKind.Loopback)]
    [InlineData(243u, 0u, true, NetworkDeviceKind.Cellular)]
    [InlineData(1u, 0u, false, NetworkDeviceKind.Other)]
    public void Kind_FromInterfaceTypeAndMedium(uint type, uint medium, bool hardware, NetworkDeviceKind expected) =>
        Assert.Equal(expected, NetworkDeviceClassifier.Kind(type, medium, hardware));

    [Fact]
    public void IsInternal_FollowsWhatWindowsShows()
    {
        Assert.True(NetworkDeviceClassifier.IsInternal(NetworkDeviceKind.Virtual, false));
        Assert.True(NetworkDeviceClassifier.IsInternal(NetworkDeviceKind.Ethernet, false));
        Assert.False(NetworkDeviceClassifier.IsInternal(NetworkDeviceKind.Virtual, true));
        Assert.False(NetworkDeviceClassifier.IsInternal(NetworkDeviceKind.Tunnel, true));
    }

    [Fact]
    public void IsInternal_WithoutWmi_OnlyTunnelsAndLoopback()
    {
        Assert.True(NetworkDeviceClassifier.IsInternal(NetworkDeviceKind.Tunnel, null));
        Assert.True(NetworkDeviceClassifier.IsInternal(NetworkDeviceKind.Loopback, null));
        Assert.False(NetworkDeviceClassifier.IsInternal(NetworkDeviceKind.Virtual, null));
        Assert.False(NetworkDeviceClassifier.IsInternal(NetworkDeviceKind.WiFi, null));
    }

    [Fact]
    public void AdapterVisibility_QueriesWmiOnlyWhenAdaptersChange()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), d = Guid.NewGuid();
        int queries = 0;
        IReadOnlyList<VisibleAdapter>? answer = [new(a, false), new(b, true)];
        var visibility = new AdapterVisibility(() => { queries++; return answer; });

        // Hidden and missing adapters are left out.
        Assert.Equivalent(new[] { a }, visibility.Shown([a, b, c]));
        Assert.Equal(1, queries);

        // Same adapters: no new query (WMI takes hundreds of milliseconds).
        visibility.Shown([a, c]);
        visibility.Shown([a, b, c]);
        Assert.Equal(1, queries);

        answer = [new(a, false), new(b, true), new(d, false)];
        Assert.Equivalent(new[] { a, d }, visibility.Shown([a, b, c, d]));
        Assert.Equal(2, queries);
    }

    [Fact]
    public void AdapterVisibility_BrokenWmi_ReturnsNullAndDoesNotRetryEverySample()
    {
        int queries = 0;
        var visibility = new AdapterVisibility(() => { queries++; return null; });
        Guid adapter = Guid.NewGuid();

        Assert.Null(visibility.Shown([adapter]));
        Assert.Null(visibility.Shown([adapter]));
        Assert.Equal(1, queries);
    }

    [Fact]
    public void MacAddress_HexWithHyphens_EmptyOrZeroIsNone()
    {
        Assert.Equal("6C-4C-E2-0C-A4-63", NetworkDevices.MacAddress([0x6c, 0x4c, 0xe2, 0x0c, 0xa4, 0x63]));
        Assert.Null(NetworkDevices.MacAddress([]));
        Assert.Null(NetworkDevices.MacAddress([0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void LinkSpeed_ZeroAndMaximumAreUnknown()
    {
        Assert.Equal(1_000_000_000L, NetworkDevices.LinkSpeed(1_000_000_000));
        Assert.Null(NetworkDevices.LinkSpeed(0));
        Assert.Null(NetworkDevices.LinkSpeed(ulong.MaxValue));
    }

    [Theory]
    [InlineData("fe80::1c2:3%12", false)] // Link-local.
    [InlineData("fec0::1", false)] // Site-local.
    [InlineData("::1", false)]
    [InlineData("2804:14c:65a1::10", true)]
    [InlineData("fd7a:115c:a1e0::1", true)] // Tailscale.
    public void IsUsefulIPv6(string address, bool expected) =>
        Assert.Equal(expected, NetworkDevices.IsUsefulIPv6(IPAddress.Parse(address)));

    [Theory]
    [InlineData("fec0:0:0:ffff::1", false)] // Placeholder Windows sets when there is no DNS.
    [InlineData("192.168.0.1", true)]
    [InlineData("2001:4860:4860::8888", true)]
    public void IsUsefulDns(string address, bool expected) =>
        Assert.Equal(expected, NetworkDevices.IsUsefulDns(IPAddress.Parse(address)));

    [Fact]
    public void Gateway_UnspecifiedIsDropped_AndScopeIsRemoved()
    {
        Assert.False(NetworkDevices.IsUsefulGateway(IPAddress.Any));
        Assert.False(NetworkDevices.IsUsefulGateway(IPAddress.IPv6Any));
        Assert.Equal("fe80::1", NetworkDevices.WithoutScope(IPAddress.Parse("fe80::1%12")));
        Assert.Equal("192.168.0.1", NetworkDevices.WithoutScope(IPAddress.Parse("192.168.0.1")));
    }

    [Theory]
    [InlineData(2437.0, 2.4, 6)]
    [InlineData(2484.0, 2.4, 14)]
    [InlineData(2412.0, 2.4, 1)]
    [InlineData(5180.0, 5.0, 36)]
    [InlineData(5745.0, 5.0, 149)]
    [InlineData(5955.0, 6.0, 1)]
    [InlineData(5935.0, 6.0, 2)]
    [InlineData(6115.0, 6.0, 33)]
    public void Wifi_BandAndChannelFromFrequency(double megahertz, double band, int channel)
    {
        Assert.Equal(band, WifiMath.BandGhz(megahertz));
        Assert.Equal(channel, WifiMath.Channel(megahertz));
    }

    [Theory]
    [InlineData(10, 6.0, "Wi-Fi 6E (802.11ax)")] // 6E only on 6 GHz.
    [InlineData(10, 5.0, "Wi-Fi 6 (802.11ax)")]
    [InlineData(8, 5.0, "Wi-Fi 5 (802.11ac)")]
    [InlineData(11, 6.0, "Wi-Fi 7 (802.11be)")]
    [InlineData(7, 2.4, "Wi-Fi 4 (802.11n)")]
    public void Wifi_StandardFromPhyType(int phyType, double band, string expected) =>
        Assert.Equal(expected, WifiMath.Standard(phyType, band));

    [Fact]
    public void Wifi_UnknownPhyType_HasNoStandard() => Assert.Null(WifiMath.Standard(0, null));

    [Fact]
    public unsafe void WlanStructures_MatchTheWindowsSdk()
    {
        Assert.Equal(604, sizeof(Wlan.ConnectionAttributes));
        Assert.Equal(360, sizeof(Wlan.BssEntry));
        Assert.Equal(68, sizeof(Wlan.AssociationAttributes));
        Assert.Equal(520, Marshal.OffsetOf<Wlan.ConnectionAttributes>(nameof(Wlan.ConnectionAttributes.Association)).ToInt32());
        Assert.Equal(60, Marshal.OffsetOf<Wlan.AssociationAttributes>(nameof(Wlan.AssociationAttributes.RxRateKbps)).ToInt32());
        Assert.Equal(92, Marshal.OffsetOf<Wlan.BssEntry>(nameof(Wlan.BssEntry.CenterFrequencyKhz)).ToInt32());
        Assert.Equal(56, Marshal.OffsetOf<Wlan.BssEntry>(nameof(Wlan.BssEntry.Rssi)).ToInt32());
    }

    [Fact]
    public void PublicAddress_ReadsTheIpLineOfTheTrace()
    {
        Assert.Equal("200.1.2.3", PublicAddress.Parse("fl=123\nh=www.cloudflare.com\nip=200.1.2.3\nts=1\n"));
        Assert.Equal("2804:14c::1", PublicAddress.Parse("ip=2804:14c::1\r\n"));
        Assert.Null(PublicAddress.Parse("fl=1\n"));
    }
}
