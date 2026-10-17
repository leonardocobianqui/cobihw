namespace CobiHw.Core.Network;

public enum NetworkDeviceKind
{
    Ethernet,
    WiFi,
    Bluetooth,
    Virtual,
    Cellular,
    Tunnel,
    Loopback,
    Other,
}

public static class NetworkDeviceClassifier
{
    public const uint TypeEthernet = 6;
    public const uint TypePpp = 23;
    public const uint TypeLoopback = 24;
    public const uint TypeVirtual = 53;
    public const uint TypeWiFi = 71;
    public const uint TypeTunnel = 131;
    public const uint TypeMobileBroadband = 243;
    public const uint TypeMobileBroadband2 = 244;

    public const uint MediumWirelessLan = 1;
    public const uint MediumWirelessWan = 8;
    public const uint MediumNative80211 = 9;
    public const uint MediumBluetooth = 10;

    public static NetworkDeviceKind Kind(uint interfaceType, uint physicalMedium, bool isHardware) => (interfaceType, physicalMedium) switch
    {
        (TypeLoopback, _) => NetworkDeviceKind.Loopback,
        (TypeTunnel, _) => NetworkDeviceKind.Tunnel,
        (TypeWiFi, _) or (_, MediumNative80211 or MediumWirelessLan) => NetworkDeviceKind.WiFi,
        (_, MediumBluetooth) => NetworkDeviceKind.Bluetooth,
        (TypeMobileBroadband or TypeMobileBroadband2, _) or (_, MediumWirelessWan) => NetworkDeviceKind.Cellular,
        (TypeEthernet, _) when isHardware => NetworkDeviceKind.Ethernet,
        (TypeEthernet or TypeVirtual or TypePpp, _) => NetworkDeviceKind.Virtual,
        _ => NetworkDeviceKind.Other,
    };

    public static bool IsInternal(NetworkDeviceKind kind, bool? shownByWindows) =>
        shownByWindows is bool shown ? !shown : kind is NetworkDeviceKind.Loopback or NetworkDeviceKind.Tunnel;
}
