using System.ComponentModel;
using System.Runtime.InteropServices;
using CobiHw.Core.Network;

namespace CobiHw.Core.Interop;

internal static class IfTable
{
    public static List<IfRow> Read()
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
            var rows = new List<IfRow>(rowCount);

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<IpHelper.MibIfRow2>(table + IpHelper.TableRowsOffset + i * rowSize);

                if ((row.InterfaceAndOperStatusFlags & IpHelper.FlagFilterInterface) != 0)
                {
                    continue;
                }

                rows.Add(new IfRow(
                    row.InterfaceLuid,
                    row.InterfaceGuid,
                    row.Alias,
                    row.Description,
                    row.Type,
                    row.PhysicalMediumType,
                    (row.InterfaceAndOperStatusFlags & IpHelper.FlagHardwareInterface) != 0,
                    row.OperStatus == IpHelper.OperStatusUp,
                    row.InOctets,
                    row.OutOctets)
                {
                    PhysicalAddress = row.PhysicalAddress[..(int)Math.Min(row.PhysicalAddressLength, (uint)row.PhysicalAddress.Length)],
                    ReceiveLinkSpeed = row.ReceiveLinkSpeed,
                });
            }

            return rows;
        }
        finally
        {
            IpHelper.FreeMibTable(table);
        }
    }
}

internal sealed record IfRow(
    ulong Luid,
    Guid Id,
    string Name,
    string Description,
    uint InterfaceType,
    uint PhysicalMedium,
    bool IsHardware,
    bool IsUp,
    ulong ReceivedBytes,
    ulong SentBytes)
{
    public byte[] PhysicalAddress { get; init; } = [];

    public ulong ReceiveLinkSpeed { get; init; }

    public bool IsPhysicalUp => (IsHardware || PhysicalMedium == NetworkDeviceClassifier.MediumBluetooth) && IsUp;
}
