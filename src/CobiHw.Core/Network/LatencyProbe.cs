using System.Net.NetworkInformation;

namespace CobiHw.Core.Network;

/// <summary>
/// Measures ping (ICMP echo) round-trip time.
/// </summary>
public static class LatencyProbe
{
    /// <summary>Sends one ping.</summary>
    /// <param name="host">IP address or host name.</param>
    /// <param name="timeout">How long to wait for the reply.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Round-trip time in milliseconds, or <see langword="null"/> on timeout or failure.</returns>
    public static async Task<long?> PingAsync(string host, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var ping = new Ping();

        try
        {
            PingReply reply = await ping.SendPingAsync(host, timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (PingException)
        {
            return null;
        }
    }
}
