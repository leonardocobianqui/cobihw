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
