using CobiHw.App.Devices;
using CobiHw.App.Settings;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.Tests.App.Devices;

/// <summary>Network devices window: tabs, details, Wi-Fi section and the public IP lookup.</summary>
[Collection(AppState.Name)]
public sealed class NetworkDevicesViewModelTests : LocalizedTest
{
    private static readonly Guid WifiId = Guid.Parse("e1482a7e-f330-4641-a8af-98f6f2e14e50");
    private static readonly Guid TailscaleId = Guid.Parse("37217669-42da-4657-a55b-0d995d328200");
    private static readonly Guid TeredoId = Guid.Parse("00000000-0000-0000-0000-000000000131");
    private static readonly Guid Wifi2Id = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid EthernetId = Guid.Parse("22222222-3333-4444-5555-666666666666");

    private static readonly WifiStatus HomeWifi = new(WifiState.Connected, "Casa", 78, -55, 5, 36, "Wi-Fi 6 (802.11ax)", 1201, 960.5);

    private int _listCalls;
    private int _wifiReads;
    private IReadOnlyList<NetworkDeviceInfo> _listed = [Device(WifiId, "Wi-Fi", NetworkDeviceKind.WiFi), Device(TailscaleId, "Tailscale", NetworkDeviceKind.Virtual), Device(TeredoId, "Teredo", NetworkDeviceKind.Tunnel, isInternal: true), Device(Wifi2Id, "Wi-Fi 2", NetworkDeviceKind.WiFi, connected: false), Device(EthernetId, "Ethernet", NetworkDeviceKind.Ethernet)];
    private WifiStatus _wifi = HomeWifi;

    private static NetworkDeviceInfo Device(Guid id, string name, NetworkDeviceKind kind, bool isInternal = false, bool connected = true) =>
        connected
            ? new(id, name, name + " adapter", kind, isInternal, true, 617_000_000, "6C-4C-E2-0C-A4-63", ["192.168.0.7/24"], ["2804:14c::7"], ["192.168.0.1"], ["192.168.0.1", "8.8.8.8"])
            : new(id, name, name + " adapter", kind, isInternal, false, null, "6C-4C-E2-0C-A4-64", [], [], [], []);

    private static string Value(IReadOnlyList<DetailRow> rows, string label) => rows.First(row => row.Label == label).Value;

    private static IEnumerable<string> Shown(IReadOnlyList<DetailRow> rows) => rows.Where(row => row.HasValue).Select(row => row.Label);

    // Tailscale is unchecked in the settings; Teredo is internal, so it's unchecked by default.
    private NetworkDevicesViewModel Window(Func<CancellationToken, Task<string?>>? publicAddress = null) => new(
        new NetworkSettings { Devices = new NetworkDeviceChoices([new(TailscaleId, false)]) },
        () => { _listCalls++; return _listed; },
        _ => { _wifiReads++; return _wifi; },
        publicAddress ?? (_ => Task.FromResult<string?>("200.1.2.3")));

    [Fact]
    public void Opening_ShowsLoading_WithoutReadingOnTheUiThread()
    {
        using NetworkDevicesViewModel window = Window();

        Assert.True(window.IsLoading);
        Assert.False(window.HasNoTabs);
        Assert.False(window.HasTabs);
        Assert.Equal(0, _listCalls);
    }

    [Fact]
    public async Task Reload_ShowsOnlyMonitoredDevices_FirstSelected()
    {
        using NetworkDevicesViewModel window = Window();

        await window.ReloadAsync();

        Assert.False(window.IsLoading);
        Assert.Equal(new[] { "Wi-Fi", "Wi-Fi 2", "Ethernet" }, window.Tabs.Select(tab => tab.Name));
        Assert.Same(window.Tabs[0], window.SelectedTab);
        Assert.True(window.HasTabs);
        Assert.Equal(1, _listCalls);
        Assert.Equal("Wi-Fi adapter", window.Tabs[0].AdapterName);
        Assert.Equal("Ethernet adapter", window.Tabs[2].AdapterName);
    }

    [Fact]
    public async Task DisconnectedWifi_DoesNotQueryWlan()
    {
        using NetworkDevicesViewModel window = Window();

        await window.ReloadAsync();

        Assert.Equal(1, _wifiReads);
        Assert.Equal("Não conectado a uma rede Wi-Fi.", window.Tabs[1].WifiMessage);
        Assert.False(window.Tabs[1].ShowWifiRows);
    }

    [Fact]
    public async Task Details_RowsInOrder_WithStatusAndKindTogether()
    {
        using NetworkDevicesViewModel window = Window();
        await window.ReloadAsync();
        NetworkDeviceTabViewModel wifi = window.Tabs[0];

        Assert.Equal(new[] { "Status", "Adaptador", "IPv4", "IPv6", "Gateway", "DNS", "Endereço físico (MAC)", "Velocidade do link", "Tráfego agora", "Sessão do cobihw" }, wifi.Rows.Select(row => row.Label));
        Assert.Equal("Conectado · Wi-Fi", Value(wifi.Rows, "Status"));
        Assert.Equal("Wi-Fi adapter", Value(wifi.Rows, "Adaptador"));
        Assert.Equal("192.168.0.7/24", Value(wifi.Rows, "IPv4"));
        Assert.Equal("192.168.0.1" + Environment.NewLine + "8.8.8.8", Value(wifi.Rows, "DNS"));
    }

    [Fact]
    public async Task LinkSpeed_OnlyOnCable_SinceWifiShowsItsOwnRates()
    {
        using NetworkDevicesViewModel window = Window();
        await window.ReloadAsync();

        Assert.DoesNotContain("Velocidade do link", Shown(window.Tabs[0].Rows));
        Assert.Equal("617 Mbps", Value(window.Tabs[2].Rows, "Velocidade do link"));
    }

    [Fact]
    public async Task Disconnected_ShowsOnlyStatusAdapterAndMac()
    {
        using NetworkDevicesViewModel window = Window();
        await window.ReloadAsync();

        Assert.Equal(new[] { "Status", "Adaptador", "Endereço físico (MAC)" }, Shown(window.Tabs[1].Rows));
        Assert.Equal("Desconectado · Wi-Fi", Value(window.Tabs[1].Rows, "Status"));
    }

    [Fact]
    public async Task Wifi_Section_WithBandAndChannelTogether()
    {
        using NetworkDevicesViewModel window = Window();
        await window.ReloadAsync();
        NetworkDeviceTabViewModel wifi = window.Tabs[0];

        Assert.True(wifi.IsWifi);
        Assert.True(wifi.ShowWifiRows);
        Assert.Equal("Casa", Value(wifi.WifiRows, "Rede (SSID)"));
        Assert.Equal("78% (-55 dBm)", Value(wifi.WifiRows, "Sinal"));
        Assert.Equal("5 GHz · canal 36", Value(wifi.WifiRows, "Banda · canal"));
        Assert.Equal("↓ 1201 Mbps · ↑ 961 Mbps", Value(wifi.WifiRows, "Taxa do link (recepção / envio)"));
    }

    [Fact]
    public async Task Traffic_UpdatesEverySecond_WithoutListingAgain()
    {
        using NetworkDevicesViewModel window = Window();
        await window.ReloadAsync();
        NetworkDeviceTabViewModel wifi = window.Tabs[0];

        await window.UpdateAsync(new NetworkSnapshot(0, 0, 0, 0, [new DeviceTraffic(WifiId, "Wi-Fi", true, 125_000, 0, 2048, 1024, 1_048_576, 0)]));

        Assert.Equal("↓ 1,00 Mbps · ↑ 0 Mbps", Value(wifi.Rows, "Tráfego agora"));
        Assert.Equal("↓ 2,00 KB · ↑ 1,00 KB", Value(wifi.Rows, "Sessão do cobihw"));
        Assert.Equal(1, _listCalls);
        Assert.Equal(2, _wifiReads);
    }

    [Fact]
    public async Task Wifi_WithoutLocationPermission_ShowsTheWarningAndButton()
    {
        using NetworkDevicesViewModel window = Window();
        await window.ReloadAsync();
        NetworkDeviceTabViewModel wifi = window.Tabs[0];

        _wifi = WifiStatus.PermissionDenied;
        await window.UpdateAsync(new NetworkSnapshot(0, 0, 0, 0, []));

        Assert.True(wifi.ShowLocationButton);
        Assert.False(wifi.ShowWifiRows);
        Assert.Contains("Localização", wifi.WifiMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wifi_AccessPointMissingFromTheScan_HidesEmptyRows()
    {
        using NetworkDevicesViewModel window = Window();
        await window.ReloadAsync();
        NetworkDeviceTabViewModel wifi = window.Tabs[0];

        // Without its access point, values from another network must not show.
        _wifi = new(WifiState.Connected, "Casa", 78, null, null, null, null, null, null);
        await window.UpdateAsync(new NetworkSnapshot(0, 0, 0, 0, []));

        Assert.Equal(new[] { "Rede (SSID)", "Sinal" }, Shown(wifi.WifiRows));
        Assert.Equal("78%", Value(wifi.WifiRows, "Sinal"));
    }

    [Fact]
    public async Task NetworkChanged_ListsAgain_AndKeepsTheSelection()
    {
        using NetworkDevicesViewModel window = Window();
        await window.ReloadAsync();
        NetworkDeviceTabViewModel wifi = window.Tabs[0];
        int readsBefore = _wifiReads;

        _listed = [Device(WifiId, "Wi-Fi", NetworkDeviceKind.WiFi, connected: false), Device(EthernetId, "Ethernet", NetworkDeviceKind.Ethernet)];
        await window.ReloadAsync();

        Assert.Equal(2, _listCalls);
        Assert.Equal(new[] { "Wi-Fi", "Ethernet" }, window.Tabs.Select(tab => tab.Name));
        Assert.Same(wifi, window.SelectedTab);
        Assert.Equal("Desconectado · Wi-Fi", Value(wifi.Rows, "Status"));
        Assert.Equal("Não conectado a uma rede Wi-Fi.", wifi.WifiMessage);
        Assert.Equal(readsBefore, _wifiReads);
    }

    [Fact]
    public async Task UncheckingInTheSettings_RemovesTheTab_WithoutListingAgain()
    {
        using NetworkDevicesViewModel window = Window();
        _listed = [Device(WifiId, "Wi-Fi", NetworkDeviceKind.WiFi), Device(EthernetId, "Ethernet", NetworkDeviceKind.Ethernet)];
        await window.ReloadAsync();

        window.UpdateSettings(new NetworkSettings { Devices = new NetworkDeviceChoices([new(WifiId, false), new(TailscaleId, false)]) });

        Assert.Single(window.Tabs);
        Assert.Equal("Ethernet", window.SelectedTab?.Name);
        Assert.Equal(1, _listCalls);
    }

    [Fact]
    public async Task PublicAddress_OnlyWhenAsked()
    {
        using NetworkDevicesViewModel window = Window();

        await window.DiscoverPublicAddressCommand.ExecuteAsync(null);

        Assert.Equal("200.1.2.3", window.PublicAddressText);
    }

    [Fact]
    public async Task Closed_ReadingThatArrivesLater_IsIgnored()
    {
        NetworkDevicesViewModel window = Window();
        _listed = [Device(EthernetId, "Ethernet", NetworkDeviceKind.Ethernet)];
        await window.ReloadAsync();
        window.Dispose();

        _listed = [Device(WifiId, "Wi-Fi", NetworkDeviceKind.WiFi)];
        await window.ReloadAsync();

        Assert.Equal(new[] { "Ethernet" }, window.Tabs.Select(tab => tab.Name));
    }

    [Fact]
    public async Task PublicAddressError_AndNoMonitoredDevices_ShowMessages()
    {
        _listed = [];
        using NetworkDevicesViewModel window = Window(_ => throw new HttpRequestException("sem internet"));

        await window.DiscoverPublicAddressCommand.ExecuteAsync(null);
        await window.ReloadAsync();

        Assert.Equal("Não foi possível obter: sem internet", window.PublicAddressText);
        // The "nothing checked" message only shows after the list was read.
        Assert.True(window.HasNoTabs);
        Assert.False(window.IsLoading);
    }
}
