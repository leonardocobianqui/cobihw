namespace CobiHw.Core.Network;

/// <summary>Kind of network device, used for the label and default choices.</summary>
public enum NetworkDeviceKind
{
    Ethernet,
    WiFi,
    Bluetooth,
    /// <summary>VPN, virtual switch or app tunnel.</summary>
    Virtual,
    Cellular,
    /// <summary>IP tunnel (Teredo, 6to4…).</summary>
    Tunnel,
    Loopback,
    Other,
}

/// <summary>
/// Classifies interfaces from their IANA type and NDIS physical medium.
/// </summary>
public static class NetworkDeviceClassifier
{
    /// <summary>IF_TYPE_ETHERNET_CSMACD; also used by many VPNs.</summary>
    public const uint TypeEthernet = 6;
    public const uint TypePpp = 23;
    public const uint TypeLoopback = 24;
    public const uint TypeVirtual = 53;
    public const uint TypeWiFi = 71;
    public const uint TypeTunnel = 131;
    public const uint TypeMobileBroadband = 243;
    public const uint TypeMobileBroadband2 = 244;

    /// <summary>NdisPhysicalMediumWirelessLan.</summary>
    public const uint MediumWirelessLan = 1;
    public const uint MediumWirelessWan = 8;
    public const uint MediumNative80211 = 9;
    /// <summary>NdisPhysicalMediumBluetooth (Bluetooth PAN).</summary>
    public const uint MediumBluetooth = 10;

    /// <summary>Kind of an interface.</summary>
    /// <param name="interfaceType">IANA IF_TYPE value.</param>
    /// <param name="physicalMedium">NDIS physical medium.</param>
    /// <param name="isHardware">True for real hardware: an Ethernet type without it is a virtual adapter.</param>
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

    /// <summary>True for devices Windows itself hides; falls back to the kind when Windows couldn't be asked.</summary>
    public static bool IsInternal(NetworkDeviceKind kind, bool? shownByWindows) =>
        shownByWindows is bool shown ? !shown : kind is NetworkDeviceKind.Loopback or NetworkDeviceKind.Tunnel;
}
