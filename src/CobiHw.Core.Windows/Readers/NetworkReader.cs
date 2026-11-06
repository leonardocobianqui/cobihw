using System.Diagnostics;
using CobiHw.Core.Windows.Interop;

namespace CobiHw.Core.Windows.Readers;

/// <summary>
/// Reads network speed and session totals per device from the interface table (<c>GetIfTable2</c>).
/// </summary>
/// <remarks>The speed and session math is shared with Linux, in <see cref="NetworkTraffic"/>.</remarks>
internal sealed class NetworkReader
{
    private readonly NetworkTraffic _traffic;

    /// <summary>Starts a reader, resuming the saved session when it belongs to the current Windows boot.</summary>
    /// <param name="savedSession">Session saved by a previous run, or <see langword="null"/>.</param>
    public NetworkReader(NetworkSessionState? savedSession)
    {
        _traffic = new NetworkTraffic(savedSession is not null && savedSession.BelongsToCurrentBoot()
            ? savedSession
            : NetworkSessionState.CreateForCurrentBoot());
    }

    /// <summary>Reads the interface table now and returns speeds and session totals.</summary>
    public NetworkSnapshot Read() => Process(IfTable.Read(), Stopwatch.GetTimestamp());

    /// <summary>Turns interface rows into speeds and session totals. Separate from <see cref="Read"/> so it can be unit tested.</summary>
    /// <param name="rows">Interface rows from the table.</param>
    /// <param name="timestamp"><see cref="Stopwatch"/> timestamp of the sample.</param>
    /// <returns>Speeds are <see langword="null"/> on the first sample, since there's nothing to compare with yet.</returns>
    public NetworkSnapshot Process(IReadOnlyList<IfRow> rows, long timestamp) =>
        _traffic.Process(
            [.. rows.Select(row => new InterfaceSample(row.Luid, row.Id, row.Name, row.IsUp, row.IsPhysicalUp, row.ReceivedBytes, row.SentBytes))],
            timestamp);

    /// <summary>Zeroes the session totals of one device, or the widget total when <paramref name="device"/> is null.</summary>
    public void ResetSession(Guid? device) => _traffic.ResetSession(device);

    /// <summary>Copy of the session state to save on exit.</summary>
    public NetworkSessionState ExportSession() => _traffic.ExportSession();
}
