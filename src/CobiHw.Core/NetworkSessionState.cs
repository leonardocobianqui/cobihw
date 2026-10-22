namespace CobiHw.Core;

/// <summary>
/// Network totals "since the session started", saved on exit so they survive app restarts within one Windows boot.
/// </summary>
public sealed class NetworkSessionState
{
    // Boot time is derived from the clock, so small clock adjustments must not look like a reboot.
    private static readonly TimeSpan BootTimeTolerance = TimeSpan.FromMinutes(2);

    /// <summary>When Windows booted; a different boot discards the saved totals.</summary>
    public DateTime BootTimeUtc { get; set; }
    /// <summary>Uptime when saved; a lower uptime now means the PC rebooted.</summary>
    public long UptimeMillisecondsAtSave { get; set; }
    /// <summary>Widget total received (physical adapters only).</summary>
    public ulong ReceivedBytes { get; set; }
    /// <summary>Widget total sent (physical adapters only).</summary>
    public ulong SentBytes { get; set; }
    /// <summary>Last cumulative counters per interface LUID, to add only the new bytes.</summary>
    public Dictionary<ulong, InterfaceCounters> LastCounters { get; set; } = [];
    /// <summary>Totals per device, by interface GUID.</summary>
    public Dictionary<Guid, DeviceSession> Devices { get; set; } = [];

    /// <summary>Empty session tied to the current boot.</summary>
    public static NetworkSessionState CreateForCurrentBoot() => new()
    {
        BootTimeUtc = CurrentBootTimeUtc(),
    };

    /// <summary>True when the saved session is from this Windows boot, so it can be resumed.</summary>
    public bool BelongsToCurrentBoot()
    {
        long uptimeNow = Environment.TickCount64;

        if (uptimeNow < UptimeMillisecondsAtSave)
        {
            return false;
        }

        return (CurrentBootTimeUtc() - BootTimeUtc).Duration() < BootTimeTolerance;
    }

    /// <summary>Deep copy stamped with the current uptime, safe to serialize while sampling continues.</summary>
    public NetworkSessionState CopyForSave() => new()
    {
        BootTimeUtc = BootTimeUtc,
        UptimeMillisecondsAtSave = Environment.TickCount64,
        ReceivedBytes = ReceivedBytes,
        SentBytes = SentBytes,
        LastCounters = new Dictionary<ulong, InterfaceCounters>(LastCounters),
        Devices = Devices.ToDictionary(device => device.Key, device => device.Value.Copy()),
    };

    private static DateTime CurrentBootTimeUtc() =>
        DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
}

/// <summary>Cumulative byte counters of one interface.</summary>
public sealed record InterfaceCounters(ulong ReceivedBytes, ulong SentBytes);

/// <summary>Session totals of one network device.</summary>
public sealed class DeviceSession
{
    /// <summary>Bytes received by this device.</summary>
    public ulong ReceivedBytes { get; set; }
    /// <summary>Bytes sent by this device.</summary>
    public ulong SentBytes { get; set; }

    /// <summary>Independent copy for saving.</summary>
    public DeviceSession Copy() => new() { ReceivedBytes = ReceivedBytes, SentBytes = SentBytes };
}
