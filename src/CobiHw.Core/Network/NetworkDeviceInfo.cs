namespace CobiHw.Core.Network;

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
