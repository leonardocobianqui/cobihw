using CobiHw.App.Settings;
using CobiHw.Core.Network;
using static CobiHw.Tests.TestData;

namespace CobiHw.Tests.App;

/// <summary>Network tab: ping host and the monitored devices.</summary>
[Collection(AppState.Name)]
public sealed class SettingsNetworkTabTests : LocalizedTest
{
    private static readonly Guid WifiId = Guid.Parse("e1482a7e-f330-4641-a8af-98f6f2e14e50");
    private static readonly Guid TailscaleId = Guid.Parse("37217669-42da-4657-a55b-0d995d328200");
    private static readonly Guid TeredoId = Guid.Parse("00000000-0000-0000-0000-000000000131");
    private static readonly Guid LanId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid GoneId = Guid.Parse("99999999-0000-0000-0000-000000000000");

    private static readonly IReadOnlyList<NetworkDeviceInfo> Devices =
    [
        Device(WifiId, "Wi-Fi", NetworkDeviceKind.WiFi, false),
        Device(TailscaleId, "Tailscale", NetworkDeviceKind.Virtual, false),
        Device(TeredoId, "Teredo", NetworkDeviceKind.Tunnel, true),
    ];

    [Fact]
    public void PingHost_SuggestionsValidationAndTrimming()
    {
        using var settings = SettingsWindow();
        settings.SelectedTab = SettingsTab.Network;
        Assert.Equal(new[] { "8.8.8.8", "1.1.1.1" }, settings.PingSuggestions);
        Assert.Equal("8.8.8.8", settings.PingHostText);
        Assert.False(settings.HasPingHostError);

        settings.PingEnabled = true;
        settings.PingHostText = "dns.google";
        Assert.False(settings.HasPingHostError);
        Assert.True(settings.ApplyCommand.CanExecute(null));

        settings.PingHostText = "não é host";
        Assert.True(settings.HasPingHostError);
        Assert.False(settings.ApplyCommand.CanExecute(null));

        settings.PingHostText = "";
        Assert.True(settings.HasPingHostError);

        settings.PingHostText = " 1.1.1.1 ";
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.ApplyCommand.Execute(null);
        Assert.Equal("1.1.1.1", applied?.Network.PingHost);
    }

    [Fact]
    public void PingHost_IsValidatedOnlyWhilePingIsOn()
    {
        using var settings = SettingsWindow();
        settings.SelectedTab = SettingsTab.Network;

        settings.PingHostText = "bad host!";
        Assert.False(settings.HasPingHostError);

        settings.PingEnabled = true;
        Assert.True(settings.HasPingHostError);

        settings.PingHostText = "1.1.1.1";
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.True(applied.Network.PingEnabled);
        Assert.Equal("1.1.1.1", applied.Network.PingHost);
    }

    [Fact]
    public void Devices_CommonOnesCheckedAndInternalOnesHidden()
    {
        using var settings = SettingsWindow(devices: Devices);
        settings.SelectedTab = SettingsTab.Network;

        Assert.Equal(3, settings.NetworkDevices.Count);
        Assert.False(settings.ShowInternalDevices);
        Assert.False(settings.NetworkDevices[2].IsShown);
        Assert.False(settings.NetworkDevices[2].IsMonitored);
        Assert.True(settings.NetworkDevices[0].IsMonitored);
        Assert.True(settings.NetworkDevices[1].IsMonitored);
        Assert.Equal(new[] { "Soma dos dispositivos", "Wi-Fi", "Tailscale" }, settings.DefaultDeviceOptions.Select(option => option.Label));
        Assert.Null(settings.SelectedDefaultDevice?.Id);
        Assert.Equal("Tailscale adapter · Virtual (VPN, túnel de app)", settings.NetworkDevices[1].Detail);

        settings.ShowInternalDevices = true;
        Assert.True(settings.NetworkDevices[2].IsShown);
    }

    [Fact]
    public void Devices_ApplySavesOnlyTheExceptions()
    {
        using var settings = SettingsWindow(devices: Devices);
        settings.SelectedTab = SettingsTab.Network;

        settings.NetworkDevices[1].IsMonitored = false;
        Assert.Equal(2, settings.DefaultDeviceOptions.Count);
        Assert.True(settings.IsNetworkPending);

        settings.SelectedDefaultDevice = settings.DefaultDeviceOptions[1];
        settings.NetworkDevices[2].IsMonitored = true;
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.Equal(new NetworkDeviceChoices([new(TailscaleId, false), new(TeredoId, true)]), applied.Network.Devices);
        Assert.Equal(new NetworkDeviceRef(WifiId, "Wi-Fi"), applied.Network.DefaultDevice);

        // Unchecking the default device falls back to the sum.
        settings.NetworkDevices[0].IsMonitored = false;
        Assert.Null(settings.SelectedDefaultDevice?.Id);

        settings.RestoreDefaultsCommand.Execute(null);
        Assert.True(settings.NetworkDevices[0].IsMonitored);
        Assert.True(settings.NetworkDevices[1].IsMonitored);
        Assert.False(settings.NetworkDevices[2].IsMonitored);
        Assert.Null(settings.SelectedDefaultDevice?.Id);

        SetLanguage("en-US");
        Assert.Equal("Sum of devices", settings.DefaultDeviceOptions[0].Label);
        Assert.EndsWith("Virtual (VPN, app tunnel)", settings.NetworkDevices[1].Detail);
    }

    [Fact]
    public void Devices_SavedDefaultThatIsGone_StaysSelected_AndItsExceptionIsKept()
    {
        using var settings = SettingsWindow(
            Settings(network: new NetworkSettings { Devices = new NetworkDeviceChoices([new(GoneId, false)]), DefaultDevice = new NetworkDeviceRef(LanId, "Ethernet 2") }),
            devices: Devices);
        Assert.Equal("Ethernet 2 (não encontrado)", settings.SelectedDefaultDevice?.Label);
        Assert.False(settings.IsNetworkPending);

        settings.NetworkDevices[1].IsMonitored = false;
        SavedSettings? applied = null;
        settings.Applied += (_, saved) => applied = saved;
        settings.SelectedTab = SettingsTab.Network;
        settings.ApplyCommand.Execute(null);

        Assert.NotNull(applied);
        Assert.Contains(applied.Network.Devices.Overrides, choice => choice.Id == GoneId);
        Assert.Contains(applied.Network.Devices.Overrides, choice => choice.Id == TailscaleId);
    }

    [Fact]
    public void DeviceChoices_DefaultsExceptionsAndEquality()
    {
        var choices = new NetworkDeviceChoices([new(TailscaleId, false), new(TeredoId, true)]);

        Assert.True(new NetworkDeviceChoices([]).IsMonitored(WifiId, false));
        Assert.False(new NetworkDeviceChoices([]).IsMonitored(TeredoId, true));
        Assert.False(choices.IsMonitored(TailscaleId, false));
        Assert.True(choices.IsMonitored(TeredoId, true));
        Assert.Equal(choices, new NetworkDeviceChoices([new(TeredoId, true), new(TailscaleId, false)]));
        Assert.Equal(new NetworkDeviceChoice(TailscaleId, false), Assert.Single(new NetworkDeviceChoices([new(TailscaleId, true), new(TailscaleId, false)]).Overrides));
    }

    // A device as Windows lists it; disconnected ones have no addresses.
    private static NetworkDeviceInfo Device(Guid id, string name, NetworkDeviceKind kind, bool isInternal) =>
        new(id, name, name + " adapter", kind, isInternal, true, 617_000_000, "6C-4C-E2-0C-A4-63", ["192.168.0.7/24"], ["2804:14c::7"], ["192.168.0.1"], ["192.168.0.1", "8.8.8.8"]);
}
