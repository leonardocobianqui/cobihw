using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.App.Devices;

/// <summary>One device in the network devices window: general details plus a Wi-Fi section.</summary>
public sealed partial class NetworkDeviceTabViewModel : ObservableObject
{
    private readonly DetailRow _status = new(nameof(Strings.Device_Status));
    private readonly DetailRow _adapter = new(nameof(Strings.Device_Adapter));
    private readonly DetailRow _ipv4 = new(nameof(Strings.Device_IPv4));
    private readonly DetailRow _ipv6 = new(nameof(Strings.Device_IPv6));
    private readonly DetailRow _gateway = new(nameof(Strings.Device_Gateway));
    private readonly DetailRow _dns = new(nameof(Strings.Device_Dns));
    private readonly DetailRow _mac = new(nameof(Strings.Device_Mac));
    private readonly DetailRow _linkSpeed = new(nameof(Strings.Device_LinkSpeed));
    private readonly DetailRow _currentSpeed = new(nameof(Strings.Device_CurrentSpeed));
    private readonly DetailRow _session = new(nameof(Strings.Device_Session));
    private readonly DetailRow _ssid = new(nameof(Strings.Wifi_Ssid));
    private readonly DetailRow _signal = new(nameof(Strings.Wifi_Signal));
    private readonly DetailRow _bandChannel = new(nameof(Strings.Wifi_BandChannel));
    private readonly DetailRow _standard = new(nameof(Strings.Wifi_Standard));
    private readonly DetailRow _linkRates = new(nameof(Strings.Wifi_LinkRates));
    // Latest data from each source; each one can update on its own.
    private NetworkDeviceInfo? _device;
    private DeviceTraffic? _traffic;
    private WifiStatus? _wifi;

    /// <summary>Creates the tab for a device.</summary>
    /// <param name="id">Device id.</param>
    public NetworkDeviceTabViewModel(Guid id)
    {
        Id = id;
        Rows = [_status, _adapter, _ipv4, _ipv6, _gateway, _dns, _mac, _linkSpeed, _currentSpeed, _session];
        WifiRows = [_ssid, _signal, _bandChannel, _standard, _linkRates];
    }

    /// <summary>Device id.</summary>
    public Guid Id { get; }

    /// <summary>General rows, in display order.</summary>
    public IReadOnlyList<DetailRow> Rows { get; }

    /// <summary>Wi-Fi rows, shown only while connected.</summary>
    public IReadOnlyList<DetailRow> WifiRows { get; }

    /// <summary>True while the device is connected.</summary>
    public bool IsConnected => _device?.IsConnected == true;

    /// <summary>True when the Wi-Fi details should be read.</summary>
    public bool NeedsWifiReading => IsWifi && IsConnected;

    /// <summary>Connection name, e.g. "Wi-Fi".</summary>
    [ObservableProperty] private string _name = string.Empty;

    /// <summary>Adapter model.</summary>
    [ObservableProperty] private string _adapterName = string.Empty;

    /// <summary>True for Wi-Fi devices.</summary>
    [ObservableProperty] private bool _isWifi;

    /// <summary>Shows the Wi-Fi rows.</summary>
    [ObservableProperty] private bool _showWifiRows;

    /// <summary>Shows the button that opens the location settings.</summary>
    [ObservableProperty] private bool _showLocationButton;

    /// <summary>Why there are no Wi-Fi details, or null.</summary>
    [ObservableProperty] private string? _wifiMessage;

    /// <summary>Shows new device data.</summary>
    /// <param name="device">Device data.</param>
    public void Update(NetworkDeviceInfo device)
    {
        // Connection changed: the old Wi-Fi reading no longer applies.
        if (_device?.IsConnected != device.IsConnected)
        {
            _wifi = null;
        }

        _device = device;
        Refresh();
    }

    /// <summary>Shows new traffic data.</summary>
    /// <param name="traffic">Traffic of this device, or null.</param>
    public void UpdateTraffic(DeviceTraffic? traffic)
    {
        _traffic = traffic;
        RefreshTraffic();
    }

    /// <summary>Shows a new Wi-Fi reading.</summary>
    /// <param name="wifi">Wi-Fi state and details.</param>
    public void UpdateWifi(WifiStatus wifi)
    {
        _wifi = wifi;
        RefreshWifi();
    }

    /// <summary>Rebuilds labels and values after a language change.</summary>
    public void RefreshTexts()
    {
        foreach (DetailRow row in Rows.Concat(WifiRows))
        {
            row.RefreshLabel();
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_device is not NetworkDeviceInfo device)
        {
            return;
        }

        Name = device.Name;
        AdapterName = device.Description;
        _status.Value = Localizer.Format(
            Strings.Format_StatusKind,
            device.IsConnected ? Strings.Status_Connected : Strings.Status_Disconnected,
            DisplayFormat.DeviceKind(device.Kind));
        _adapter.Value = device.Description;
        _ipv4.Value = DisplayFormat.Lines(device.IPv4Addresses);
        _ipv6.Value = DisplayFormat.Lines(device.IPv6Addresses);
        _gateway.Value = DisplayFormat.Lines(device.Gateways);
        _dns.Value = DisplayFormat.Lines(device.DnsServers);
        _mac.Value = device.MacAddress ?? string.Empty;
        // Wi-Fi link rates are in the Wi-Fi section.
        _linkSpeed.Value = device.Kind == NetworkDeviceKind.Ethernet && device.LinkSpeedBitsPerSecond is long bits
            ? DisplayFormat.LinkRate(bits / 1_000_000.0)
            : string.Empty;
        IsWifi = device.Kind == NetworkDeviceKind.WiFi;
        RefreshTraffic();
        RefreshWifi();
    }

    private void RefreshTraffic()
    {
        if (!IsConnected)
        {
            _currentSpeed.Value = string.Empty;
            _session.Value = string.Empty;
            return;
        }

        _currentSpeed.Value = DisplayFormat.UpDown(
            DisplayFormat.NetworkRate(_traffic?.DownloadBytesPerSecond),
            DisplayFormat.NetworkRate(_traffic?.UploadBytesPerSecond));
        _session.Value = _traffic is null
            ? DisplayFormat.Unavailable
            : DisplayFormat.UpDown(DisplayFormat.Size(_traffic.SessionReceivedBytes), DisplayFormat.Size(_traffic.SessionSentBytes));
    }

    // Recent Windows versions need location permission to read Wi-Fi details.
    private void RefreshWifi()
    {
        WifiStatus? wifi = !IsWifi ? null : IsConnected ? _wifi : WifiStatus.Disconnected;

        ShowWifiRows = wifi?.State == WifiState.Connected;
        ShowLocationButton = wifi?.State == WifiState.PermissionDenied;
        WifiMessage = wifi?.State switch
        {
            WifiState.Disconnected => Strings.Wifi_NotConnected,
            WifiState.PermissionDenied => Strings.Wifi_PermissionNeeded,
            WifiState.Unavailable => Strings.Wifi_Unavailable,
            _ => null,
        };

        _ssid.Value = wifi?.Ssid ?? string.Empty;
        _signal.Value = wifi?.SignalPercent is int percent
            ? wifi.RssiDbm is int rssi ? Localizer.Format(Strings.Format_Signal, percent, rssi) : Localizer.Format("{0}%", percent)
            : string.Empty;
        _bandChannel.Value = wifi?.BandGhz is double band && wifi.Channel is int channel
            ? Localizer.Format(Strings.Format_BandChannel, band, channel)
            : string.Empty;
        _standard.Value = wifi?.Standard ?? string.Empty;
        _linkRates.Value = wifi?.ReceiveMbps is double receive && wifi.SendMbps is double send
            ? DisplayFormat.UpDown(DisplayFormat.LinkRate(receive), DisplayFormat.LinkRate(send))
            : string.Empty;
    }
}
