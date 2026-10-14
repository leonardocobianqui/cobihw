using System.Net.NetworkInformation;

namespace CobiHw.Core.Network;

public static class LatencyProbe
{
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
