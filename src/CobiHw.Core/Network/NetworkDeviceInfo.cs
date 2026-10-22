namespace CobiHw.Core.Network;

/// <summary>Details of one network device for the settings and "Network devices" windows.</summary>
/// <param name="Id">Interface GUID.</param>
/// <param name="Name">Alias shown in Windows settings.</param>
/// <param name="Description">Driver description.</param>
/// <param name="Kind">Ethernet, Wi-Fi, VPN…</param>
/// <param name="IsInternal">Hidden by Windows (filters, loopback…); unchecked by default.</param>
/// <param name="IsConnected">Operational status is up.</param>
/// <param name="LinkSpeedBitsPerSecond">Negotiated link speed, or null when disconnected or unknown.</param>
/// <param name="MacAddress">"AA-BB-CC-DD-EE-FF", or null for virtual interfaces.</param>
/// <param name="IPv4Addresses">Addresses with prefix, e.g. "192.168.0.10/24".</param>
/// <param name="IPv6Addresses">Global addresses only (link-local left out).</param>
/// <param name="Gateways">Default gateways.</param>
/// <param name="DnsServers">DNS servers.</param>
public sealed record NetworkDeviceInfo(
    Guid Id,
    string Name,
    string Description,
    NetworkDeviceKind Kind,
    bool IsInternal,
    bool IsConnected,
    long? LinkSpeedBitsPerSecond,
    string? MacAddress,
    IReadOnlyList<string> IPv4Addresses,
    IReadOnlyList<string> IPv6Addresses,
    IReadOnlyList<string> Gateways,
    IReadOnlyList<string> DnsServers);
