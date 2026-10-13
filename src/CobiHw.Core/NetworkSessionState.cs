namespace CobiHw.Core;

public sealed class NetworkSessionState
{
    private static readonly TimeSpan BootTimeTolerance = TimeSpan.FromMinutes(2);

    public DateTime BootTimeUtc { get; set; }
    public long UptimeMillisecondsAtSave { get; set; }
    public ulong ReceivedBytes { get; set; }
    public ulong SentBytes { get; set; }
    public Dictionary<ulong, InterfaceCounters> LastCounters { get; set; } = [];

    public static NetworkSessionState CreateForCurrentBoot() => new()
    {
        BootTimeUtc = CurrentBootTimeUtc(),
    };

    public bool BelongsToCurrentBoot()
    {
        long uptimeNow = Environment.TickCount64;

        if (uptimeNow < UptimeMillisecondsAtSave)
        {
            return false;
        }

        return (CurrentBootTimeUtc() - BootTimeUtc).Duration() < BootTimeTolerance;
    }

    public NetworkSessionState CopyForSave() => new()
    {
        BootTimeUtc = BootTimeUtc,
        UptimeMillisecondsAtSave = Environment.TickCount64,
        ReceivedBytes = ReceivedBytes,
        SentBytes = SentBytes,
        LastCounters = new Dictionary<ulong, InterfaceCounters>(LastCounters),
    };

    private static DateTime CurrentBootTimeUtc() =>
        DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
}

public sealed record InterfaceCounters(ulong ReceivedBytes, ulong SentBytes);
