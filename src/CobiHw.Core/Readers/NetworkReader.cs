using System.Diagnostics;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

internal sealed class NetworkReader
{
    private readonly NetworkSessionState _session;
    private Dictionary<ulong, InterfaceCounters> _previousSample = [];
    private long _previousTimestamp;

    public NetworkReader(NetworkSessionState? savedSession)
    {
        _session = savedSession is not null && savedSession.BelongsToCurrentBoot()
            ? savedSession
            : NetworkSessionState.CreateForCurrentBoot();
    }

    public NetworkSnapshot Read() => Process(IfTable.Read(), Stopwatch.GetTimestamp());

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

    public NetworkSessionState ExportSession() => _session.CopyForSave();

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

    private static ulong Delta(ulong current, ulong previous) =>
        current >= previous ? current - previous : current;
}
