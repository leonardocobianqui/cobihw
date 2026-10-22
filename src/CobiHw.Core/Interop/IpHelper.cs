using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

/// <summary>
/// iphlpapi.dll declarations for the network interface table.
/// </summary>
internal static partial class IpHelper
{
    /// <summary>MIB_IF_TABLE2 starts with a 4-byte count, padded to 8 before the first row.</summary>
    public const int TableRowsOffset = 8;
    /// <summary>InterfaceAndOperStatusFlags: backed by real hardware.</summary>
    public const byte FlagHardwareInterface = 0x01;
    /// <summary>InterfaceAndOperStatusFlags: filter driver layer (duplicates a real adapter).</summary>
    public const byte FlagFilterInterface = 0x02;
    /// <summary>IfOperStatusUp.</summary>
    public const uint OperStatusUp = 1;

    /// <summary>Mirrors MIB_IF_ROW2. Only a few fields are used, but the layout must match exactly.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MibIfRow2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Alias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Description;
        public uint PhysicalAddressLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PhysicalAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PermanentPhysicalAddress;
        public uint Mtu;
        public uint Type;
        public uint TunnelType;
        public uint MediaType;
        public uint PhysicalMediumType;
        public uint AccessType;
        public uint DirectionType;
        public byte InterfaceAndOperStatusFlags;
        public uint OperStatus;
        public uint AdminStatus;
        public uint MediaConnectState;
        public Guid NetworkGuid;
        public uint ConnectionType;
        public ulong TransmitLinkSpeed;
        public ulong ReceiveLinkSpeed;
        public ulong InOctets;
        public ulong InUcastPkts;
        public ulong InNUcastPkts;
        public ulong InDiscards;
        public ulong InErrors;
        public ulong InUnknownProtos;
        public ulong InUcastOctets;
        public ulong InMulticastOctets;
        public ulong InBroadcastOctets;
        public ulong OutOctets;
        public ulong OutUcastPkts;
        public ulong OutNUcastPkts;
        public ulong OutDiscards;
        public ulong OutErrors;
        public ulong OutUcastOctets;
        public ulong OutMulticastOctets;
        public ulong OutBroadcastOctets;
        public ulong OutQLen;
    }

    /// <summary>Allocates and fills the interface table; free it with <see cref="FreeMibTable"/>.</summary>
    [LibraryImport("iphlpapi.dll")]
    public static partial uint GetIfTable2(out nint table);

    /// <summary>Frees memory returned by <see cref="GetIfTable2"/>.</summary>
    [LibraryImport("iphlpapi.dll")]
    public static partial void FreeMibTable(nint memory);
}
