using System.Net.Http;

namespace CobiHw.Core.Network;

public static class PublicAddress
{
    public const string TraceUrl = "https://www.cloudflare.com/cdn-cgi/trace";

    private const string AddressKey = "ip=";

    public static async Task<string?> FetchAsync(HttpClient http, CancellationToken cancellationToken)
    {
        string trace = await http.GetStringAsync(TraceUrl, cancellationToken);
        return Parse(trace);
    }

    public static string? Parse(string trace) =>
        trace.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(AddressKey, StringComparison.Ordinal))
            .Select(line => line[AddressKey.Length..])
            .FirstOrDefault(address => address.Length > 0);
}
