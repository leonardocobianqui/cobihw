using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

    public NetworkSnapshot Read()
    {
        Dictionary<ulong, InterfaceCounters> current = ReadPhysicalInterfaces();
        long now = Stopwatch.GetTimestamp();

        var (download, upload) = CalculateSpeed(current, now);
        AccumulateSession(current);

        _previousSample = current;
        _previousTimestamp = now;

        return new NetworkSnapshot(download, upload, _session.ReceivedBytes, _session.SentBytes);
    }

    public void ResetSession()
    {
        _session.ReceivedBytes = 0;
        _session.SentBytes = 0;
        _session.LastCounters = ReadPhysicalInterfaces();
    }

    public NetworkSessionState ExportSession() => _session.CopyForSave();

    private (double? Download, double? Upload) CalculateSpeed(Dictionary<ulong, InterfaceCounters> current, long now)
    {
        if (_previousTimestamp == 0)
        {
            return (null, null);
        }

        double seconds = Stopwatch.GetElapsedTime(_previousTimestamp, now).TotalSeconds;
        ulong received = 0;
        ulong sent = 0;

        foreach (var (luid, counters) in current)
        {
            if (_previousSample.TryGetValue(luid, out InterfaceCounters? previous))
            {
                received += Delta(counters.ReceivedBytes, previous.ReceivedBytes);
                sent += Delta(counters.SentBytes, previous.SentBytes);
            }
        }

        return (received / seconds, sent / seconds);
    }

    private void AccumulateSession(Dictionary<ulong, InterfaceCounters> current)
    {
        foreach (var (luid, counters) in current)
        {
            InterfaceCounters last = _session.LastCounters.GetValueOrDefault(luid) ?? new InterfaceCounters(0, 0);

            _session.ReceivedBytes += Delta(counters.ReceivedBytes, last.ReceivedBytes);
            _session.SentBytes += Delta(counters.SentBytes, last.SentBytes);
            _session.LastCounters[luid] = counters;
        }
    }

    private static ulong Delta(ulong current, ulong previous) =>
        current >= previous ? current - previous : current;

    private static Dictionary<ulong, InterfaceCounters> ReadPhysicalInterfaces()
    {
        uint status = IpHelper.GetIfTable2(out nint table);
        if (status != 0)
        {
            throw new Win32Exception((int)status);
        }

        try
        {
            int rowCount = Marshal.ReadInt32(table);
            int rowSize = Marshal.SizeOf<IpHelper.MibIfRow2>();
            var interfaces = new Dictionary<ulong, InterfaceCounters>();

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<IpHelper.MibIfRow2>(table + IpHelper.TableRowsOffset + i * rowSize);

                if (IsActivePhysicalAdapter(row))
                {
                    interfaces[row.InterfaceLuid] = new InterfaceCounters(row.InOctets, row.OutOctets);
                }
            }

            return interfaces;
        }
        finally
        {
            IpHelper.FreeMibTable(table);
        }
    }

    private static bool IsActivePhysicalAdapter(IpHelper.MibIfRow2 row)
    {
        bool isHardware = (row.InterfaceAndOperStatusFlags & IpHelper.FlagHardwareInterface) != 0;
        bool isFilter = (row.InterfaceAndOperStatusFlags & IpHelper.FlagFilterInterface) != 0;

        return isHardware && !isFilter && row.OperStatus == IpHelper.OperStatusUp;
    }
}
