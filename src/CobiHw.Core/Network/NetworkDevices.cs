using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Network;

public static class NetworkDevices
{
    private static readonly AdapterVisibility Visibility = new(NetAdapterQuery.Read);

    public static IReadOnlyList<NetworkDeviceInfo> List()
    {
        List<IfRow> rows = [.. IfTable.Read().DistinctBy(row => row.Id)];
        Dictionary<Guid, NetworkInterface> adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Select(adapter => (Adapter: adapter, Valid: Guid.TryParse(adapter.Id, out Guid id), Id: id))
            .Where(entry => entry.Valid)
            .DistinctBy(entry => entry.Id)
            .ToDictionary(entry => entry.Id, entry => entry.Adapter);
        IReadOnlySet<Guid>? shown = Visibility.Shown([.. rows.Select(row => row.Id)]);

        return [.. rows
            .Select(row => Describe(row, adapters.GetValueOrDefault(row.Id), shown?.Contains(row.Id)))
            .OrderBy(device => device.IsInternal)
            .ThenByDescending(device => device.IsConnected)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public static string? MacAddress(IReadOnlyList<byte> bytes) =>
        bytes.Count == 0 || bytes.All(value => value == 0) ? null : string.Join('-', bytes.Select(value => value.ToString("X2")));

    public static long? LinkSpeed(ulong bitsPerSecond) =>
        bitsPerSecond is 0 or > long.MaxValue ? null : (long)bitsPerSecond;

    public static bool IsUsefulIPv6(IPAddress address) =>
        !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal && !IPAddress.IsLoopback(address);

    public static bool IsUsefulDns(IPAddress address) => !address.IsIPv6SiteLocal;

    public static bool IsUsefulGateway(IPAddress address) =>
        !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any);

    public static string WithoutScope(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? new IPAddress(address.GetAddressBytes()).ToString() : address.ToString();

    private static NetworkDeviceInfo Describe(IfRow row, NetworkInterface? adapter, bool? shownByWindows)
    {
        NetworkDeviceKind kind = NetworkDeviceClassifier.Kind(row.InterfaceType, row.PhysicalMedium, row.IsHardware);
        IPInterfaceProperties? properties = row.IsUp ? adapter?.GetIPProperties() : null;
        List<UnicastIPAddressInformation> unicast = properties is null
            ? []
            : [.. properties.UnicastAddresses.Where(address => address.DuplicateAddressDetectionState == DuplicateAddressDetectionState.Preferred)];
        IEnumerable<IPAddress> gateways = properties?.GatewayAddresses.Select(gateway => gateway.Address) ?? Enumerable.Empty<IPAddress>();
        IEnumerable<IPAddress> dnsServers = properties?.DnsAddresses ?? Enumerable.Empty<IPAddress>();

        return new NetworkDeviceInfo(
            row.Id,
            row.Name,
            row.Description,
            kind,
            NetworkDeviceClassifier.IsInternal(kind, shownByWindows),
            row.IsUp,
            row.IsUp ? LinkSpeed(row.ReceiveLinkSpeed) : null,
            MacAddress(row.PhysicalAddress),
            [.. unicast
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => $"{address.Address}/{address.PrefixLength}")],
            [.. unicast
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetworkV6 && IsUsefulIPv6(address.Address))
                .Select(address => WithoutScope(address.Address))],
            [.. gateways.Where(IsUsefulGateway).Select(WithoutScope).Distinct()],
            [.. dnsServers.Where(IsUsefulDns).Select(WithoutScope).Distinct()]);
    }
}
