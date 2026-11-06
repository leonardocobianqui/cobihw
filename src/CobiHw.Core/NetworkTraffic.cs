using System.Diagnostics;

namespace CobiHw.Core;

/// <summary>One network interface as a platform reader sees it in a sample.</summary>
/// <param name="Key">Identifies the interface within this boot: LUID on Windows, ifindex on Linux. Keys the saved counters.</param>
/// <param name="Id">Stable device id, saved in the settings and in the session totals.</param>
/// <param name="Name">Name shown to the user.</param>
/// <param name="IsUp">True when connected.</param>
/// <param name="CountsInTotal">True for physical adapters that are up; VPNs and tunnels stay out so their traffic isn't counted twice.</param>
/// <param name="ReceivedBytes">Cumulative counter since the interface came up.</param>
/// <param name="SentBytes">Cumulative counter since the interface came up.</param>
public sealed record InterfaceSample(ulong Key, Guid Id, string Name, bool IsUp, bool CountsInTotal, ulong ReceivedBytes, ulong SentBytes);

/// <summary>
/// Turns cumulative interface counters into speeds and session totals, the same way on every platform.
/// </summary>
/// <remarks>
/// Speeds come from the byte difference between two samples. Session totals count everything since the session
/// started and survive app restarts within the same boot, through <see cref="NetworkSessionState"/>.
/// </remarks>
public sealed class NetworkTraffic(NetworkSessionState session)
{
    // Counters from the previous sample, by interface key, used to compute speeds.
    private Dictionary<ulong, InterfaceCounters> _previousSample = [];
    private long _previousTimestamp;

    /// <summary>Speeds and session totals for this sample.</summary>
    /// <param name="interfaces">Every interface read now.</param>
    /// <param name="timestamp"><see cref="Stopwatch"/> timestamp of the sample.</param>
    /// <returns>Speeds are <see langword="null"/> on the first sample, since there's nothing to compare with yet.</returns>
    public NetworkSnapshot Process(IReadOnlyList<InterfaceSample> interfaces, long timestamp)
    {
        double? seconds = _previousTimestamp == 0 ? null : Stopwatch.GetElapsedTime(_previousTimestamp, timestamp).TotalSeconds;
        var current = new Dictionary<ulong, InterfaceCounters>();
        var devices = new List<DeviceTraffic>(interfaces.Count);
        ulong totalReceived = 0;
        ulong totalSent = 0;

        foreach (InterfaceSample sample in interfaces)
        {
            var counters = new InterfaceCounters(sample.ReceivedBytes, sample.SentBytes);
            current[sample.Key] = counters;

            InterfaceCounters? previous = _previousSample.GetValueOrDefault(sample.Key);
            ulong receivedDelta = previous is null ? 0 : Delta(counters.ReceivedBytes, previous.ReceivedBytes);
            ulong sentDelta = previous is null ? 0 : Delta(counters.SentBytes, previous.SentBytes);

            if (sample.CountsInTotal)
            {
                totalReceived += receivedDelta;
                totalSent += sentDelta;
            }

            DeviceSession deviceSession = Accumulate(sample, counters);
            bool measured = seconds is not null && previous is not null;

            devices.Add(new DeviceTraffic(
                sample.Id,
                sample.Name,
                sample.IsUp,
                measured ? receivedDelta / seconds!.Value : null,
                measured ? sentDelta / seconds!.Value : null,
                deviceSession.ReceivedBytes,
                deviceSession.SentBytes,
                counters.ReceivedBytes,
                counters.SentBytes));
        }

        _previousSample = current;
        _previousTimestamp = timestamp;

        return new NetworkSnapshot(
            seconds is null ? null : totalReceived / seconds.Value,
            seconds is null ? null : totalSent / seconds.Value,
            session.ReceivedBytes,
            session.SentBytes,
            devices);
    }

    /// <summary>Zeroes the session totals of one device, or the widget total when <paramref name="device"/> is null.</summary>
    public void ResetSession(Guid? device)
    {
        if (device is Guid id)
        {
            session.Devices[id] = new DeviceSession();
            return;
        }

        session.ReceivedBytes = 0;
        session.SentBytes = 0;
    }

    /// <summary>Copy of the session state to save on exit.</summary>
    public NetworkSessionState ExportSession() => session.CopyForSave();

    // Adds the bytes since the last sample to the session totals (widget total and per device).
    private DeviceSession Accumulate(InterfaceSample sample, InterfaceCounters counters)
    {
        InterfaceCounters last = session.LastCounters.GetValueOrDefault(sample.Key) ?? new InterfaceCounters(0, 0);
        ulong received = Delta(counters.ReceivedBytes, last.ReceivedBytes);
        ulong sent = Delta(counters.SentBytes, last.SentBytes);

        if (sample.CountsInTotal)
        {
            session.ReceivedBytes += received;
            session.SentBytes += sent;
        }

        if (!session.Devices.TryGetValue(sample.Id, out DeviceSession? deviceSession))
        {
            deviceSession = new DeviceSession();
            session.Devices[sample.Id] = deviceSession;
        }

        deviceSession.ReceivedBytes += received;
        deviceSession.SentBytes += sent;
        session.LastCounters[sample.Key] = counters;
        return deviceSession;
    }

    // A counter lower than before means the interface was reset (e.g. adapter re-enabled): count from zero.
    private static ulong Delta(ulong current, ulong previous) =>
        current >= previous ? current - previous : current;
}
