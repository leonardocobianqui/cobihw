using System.ComponentModel;
using System.Runtime.InteropServices;
using CobiHw.Core.Network;

namespace CobiHw.Core.Interop;

/// <summary>
/// Reads the network interface table into managed <see cref="IfRow"/> records.
/// </summary>
internal static class IfTable
{
    /// <summary>Reads every interface except filter layers.</summary>
    /// <exception cref="System.ComponentModel.Win32Exception">GetIfTable2 failed.</exception>
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

                // Filter layers (e.g. QoS, firewalls) repeat the real adapter's traffic.
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

/// <summary>
/// One network interface with its cumulative byte counters.
/// </summary>
/// <param name="Luid">Locally unique id; changes when the adapter is reinstalled.</param>
/// <param name="Id">Interface GUID, stable across reboots; used to save choices.</param>
/// <param name="Name">Alias shown in Windows settings (e.g. "Wi-Fi").</param>
/// <param name="Description">Driver description (e.g. "Intel(R) Wi-Fi 6 AX201").</param>
/// <param name="InterfaceType">IANA interface type (IF_TYPE_*).</param>
/// <param name="PhysicalMedium">NDIS physical medium (NdisPhysicalMedium*).</param>
/// <param name="IsHardware">Backed by real hardware.</param>
/// <param name="IsUp">Operational status is up.</param>
/// <param name="ReceivedBytes">Bytes received since the interface came up.</param>
/// <param name="SentBytes">Bytes sent since the interface came up.</param>
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
    /// <summary>MAC address (empty for virtual interfaces without one).</summary>
    public byte[] PhysicalAddress { get; init; } = [];

    /// <summary>Link speed in bits per second, as negotiated.</summary>
    public ulong ReceiveLinkSpeed { get; init; }

    /// <summary>Counts toward the widget total: real hardware (Bluetooth PAN included) that is up.</summary>
    public bool IsPhysicalUp => (IsHardware || PhysicalMedium == NetworkDeviceClassifier.MediumBluetooth) && IsUp;
}
