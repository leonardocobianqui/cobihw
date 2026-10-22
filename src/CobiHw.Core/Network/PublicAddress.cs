using System.Net.Http;

namespace CobiHw.Core.Network;

/// <summary>
/// Finds the public IP address the internet sees, using Cloudflare's trace endpoint.
/// </summary>
public static class PublicAddress
{
    /// <summary>Returns plain "key=value" lines, including "ip=".</summary>
    public const string TraceUrl = "https://www.cloudflare.com/cdn-cgi/trace";

    private const string AddressKey = "ip=";

    /// <summary>Asks Cloudflare which address the main route leaves from. Only called when the user clicks.</summary>
    /// <returns>The address, or <see langword="null"/> if the answer has none.</returns>
    /// <exception cref="HttpRequestException">Cloudflare couldn't be reached.</exception>
    public static async Task<string?> FetchAsync(HttpClient http, CancellationToken cancellationToken)
    {
        string trace = await http.GetStringAsync(TraceUrl, cancellationToken);
        return Parse(trace);
    }

    /// <summary>Extracts the "ip=" value from a trace response.</summary>
    public static string? Parse(string trace) =>
        trace.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(AddressKey, StringComparison.Ordinal))
            .Select(line => line[AddressKey.Length..])
            .FirstOrDefault(address => address.Length > 0);
}
