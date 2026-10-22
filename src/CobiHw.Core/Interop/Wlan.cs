using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

/// <summary>
/// wlanapi.dll declarations used to read the Wi-Fi network name, signal, band and channel.
/// </summary>
internal static unsafe partial class Wlan
{
    /// <summary>Client version for Windows Vista and later.</summary>
    public const uint ClientVersion = 2;
    /// <summary>wlan_intf_opcode_current_connection.</summary>
    public const int OpcodeCurrentConnection = 7;
    public const int StateConnected = 1;
    public const int BssTypeInfrastructure = 1;
    /// <summary>Returned when Location access is off: since Windows 11 24H2, Wi-Fi details need it.</summary>
    public const uint ErrorAccessDenied = 5;
    /// <summary>WLAN_BSS_LIST: total size and item count come before the entries.</summary>
    public const int BssListHeaderSize = 8;

    /// <summary>Mirrors DOT11_SSID; the name is raw bytes, usually UTF-8.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Dot11Ssid
    {
        public uint Length;
        public fixed byte Bytes[32];
    }

    /// <summary>Mirrors WLAN_ASSOCIATION_ATTRIBUTES.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AssociationAttributes
    {
        public Dot11Ssid Ssid;
        public int BssType;
        public fixed byte Bssid[6];
        public int PhyType;
        public uint PhyIndex;
        /// <summary>0 to 100.</summary>
        public uint SignalQuality;
        public uint RxRateKbps;
        public uint TxRateKbps;
    }

    /// <summary>Mirrors WLAN_SECURITY_ATTRIBUTES.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SecurityAttributes
    {
        public int SecurityEnabled;
        public int OneXEnabled;
        public int AuthAlgorithm;
        public int CipherAlgorithm;
    }

    /// <summary>Mirrors WLAN_CONNECTION_ATTRIBUTES.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ConnectionAttributes
    {
        public int State;
        public int ConnectionMode;
        public fixed char ProfileName[256];
        public AssociationAttributes Association;
        public SecurityAttributes Security;
    }

    /// <summary>Mirrors WLAN_BSS_ENTRY; used for RSSI and channel frequency.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BssEntry
    {
        public Dot11Ssid Ssid;
        public uint PhyId;
        public fixed byte Bssid[6];
        public int BssType;
        public int PhyType;
        public int Rssi;
        public uint LinkQuality;
        public byte InRegulatoryDomain;
        public ushort BeaconPeriod;
        public ulong Timestamp;
        public ulong HostTimestamp;
        public ushort CapabilityInformation;
        public uint CenterFrequencyKhz;
        public uint RateSetLength;
        public fixed ushort RateSet[126];
        public uint IeOffset;
        public uint IeSize;
    }

    /// <summary>Opens a WLAN client handle; close it with <see cref="WlanCloseHandle"/>.</summary>
    [LibraryImport("wlanapi.dll")]
    public static partial uint WlanOpenHandle(uint clientVersion, nint reserved, out uint negotiatedVersion, out nint clientHandle);

    /// <summary>Closes a WLAN client handle.</summary>
    [LibraryImport("wlanapi.dll")]
    public static partial uint WlanCloseHandle(nint clientHandle, nint reserved);

    /// <summary>Queries one property of a wireless interface; free <paramref name="data"/> with <see cref="WlanFreeMemory"/>.</summary>
    [LibraryImport("wlanapi.dll")]
    public static partial uint WlanQueryInterface(nint clientHandle, in Guid interfaceGuid, int opCode, nint reserved, out uint dataSize, out nint data, out int opcodeValueType);

    /// <summary>Lists the access points seen for an SSID; free <paramref name="bssList"/> with <see cref="WlanFreeMemory"/>.</summary>
    [LibraryImport("wlanapi.dll")]
    public static partial uint WlanGetNetworkBssList(nint clientHandle, in Guid interfaceGuid, Dot11Ssid* ssid, int bssType, int securityEnabled, nint reserved, out nint bssList);

    /// <summary>Frees memory returned by the WLAN API.</summary>
    [LibraryImport("wlanapi.dll")]
    public static partial void WlanFreeMemory(nint memory);
}
