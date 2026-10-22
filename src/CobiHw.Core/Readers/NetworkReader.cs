using System.Diagnostics;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

/// <summary>
/// Reads network speed and session totals per device from the interface table (<c>GetIfTable2</c>).
/// </summary>
/// <remarks>
/// Speeds come from the byte difference between two samples. Session totals count everything since the session
/// started and survive app restarts within the same Windows boot.
/// </remarks>
internal sealed class NetworkReader
{
    private readonly NetworkSessionState _session;
    // Counters from the previous sample, by interface LUID, used to compute speeds.
    private Dictionary<ulong, InterfaceCounters> _previousSample = [];
    private long _previousTimestamp;

    /// <summary>Starts a reader, resuming the saved session when it belongs to the current Windows boot.</summary>
    /// <param name="savedSession">Session saved by a previous run, or <see langword="null"/>.</param>
    public NetworkReader(NetworkSessionState? savedSession)
    {
        _session = savedSession is not null && savedSession.BelongsToCurrentBoot()
            ? savedSession
            : NetworkSessionState.CreateForCurrentBoot();
    }

    /// <summary>Reads the interface table now and returns speeds and session totals.</summary>
    public NetworkSnapshot Read() => Process(IfTable.Read(), Stopwatch.GetTimestamp());

    /// <summary>Turns interface counters into speeds and session totals. Separate from <see cref="Read"/> so it can be unit tested.</summary>
    /// <param name="rows">Interface rows from the table.</param>
    /// <param name="timestamp"><see cref="Stopwatch"/> timestamp of the sample.</param>
    /// <returns>Speeds are <see langword="null"/> on the first sample, since there's nothing to compare with yet.</returns>
    public NetworkSnapshot Process(IReadOnlyList<IfRow> rows, long timestamp)
    {
        double? seconds = _previousTimestamp == 0 ? null : Stopwatch.GetElapsedTime(_previousTimestamp, timestamp).TotalSeconds;
        var current = new Dictionary<ulong, InterfaceCounters>();
        var devices = new List<DeviceTraffic>(rows.Count);
        ulong physicalReceived = 0;
        ulong physicalSent = 0;

        foreach (IfRow row in rows)
        {
            var counters = new InterfaceCounters(row.ReceivedBytes, row.SentBytes);
            current[row.Luid] = counters;

            InterfaceCounters? previous = _previousSample.GetValueOrDefault(row.Luid);
            ulong receivedDelta = previous is null ? 0 : Delta(counters.ReceivedBytes, previous.ReceivedBytes);
            ulong sentDelta = previous is null ? 0 : Delta(counters.SentBytes, previous.SentBytes);

            // The widget total counts only physical adapters, so VPN traffic isn't counted twice.
            if (row.IsPhysicalUp)
            {
                physicalReceived += receivedDelta;
                physicalSent += sentDelta;
            }

            DeviceSession session = Accumulate(row, counters);
            bool measured = seconds is not null && previous is not null;

            devices.Add(new DeviceTraffic(
                row.Id,
                row.Name,
                row.IsUp,
                measured ? receivedDelta / seconds!.Value : null,
                measured ? sentDelta / seconds!.Value : null,
                session.ReceivedBytes,
                session.SentBytes,
                counters.ReceivedBytes,
                counters.SentBytes));
        }

        _previousSample = current;
        _previousTimestamp = timestamp;

        return new NetworkSnapshot(
            seconds is null ? null : physicalReceived / seconds.Value,
            seconds is null ? null : physicalSent / seconds.Value,
            _session.ReceivedBytes,
            _session.SentBytes,
            devices);
    }

    /// <summary>Zeroes the session totals of one device, or the widget total when <paramref name="device"/> is null.</summary>
    public void ResetSession(Guid? device)
    {
        if (device is Guid id)
        {
            _session.Devices[id] = new DeviceSession();
            return;
        }

        _session.ReceivedBytes = 0;
        _session.SentBytes = 0;
    }

    /// <summary>Copy of the session state to save on exit.</summary>
    public NetworkSessionState ExportSession() => _session.CopyForSave();

    // Adds the bytes since the last sample to the session totals (widget total and per device).
    private DeviceSession Accumulate(IfRow row, InterfaceCounters counters)
    {
        InterfaceCounters last = _session.LastCounters.GetValueOrDefault(row.Luid) ?? new InterfaceCounters(0, 0);
        ulong received = Delta(counters.ReceivedBytes, last.ReceivedBytes);
        ulong sent = Delta(counters.SentBytes, last.SentBytes);

        if (row.IsPhysicalUp)
        {
            _session.ReceivedBytes += received;
            _session.SentBytes += sent;
        }

        if (!_session.Devices.TryGetValue(row.Id, out DeviceSession? session))
        {
            session = new DeviceSession();
            _session.Devices[row.Id] = session;
        }

        session.ReceivedBytes += received;
        session.SentBytes += sent;
        _session.LastCounters[row.Luid] = counters;
        return session;
    }

    // A counter lower than before means the interface was reset (e.g. adapter re-enabled): count from zero.
    private static ulong Delta(ulong current, ulong previous) =>
        current >= previous ? current - previous : current;
}
