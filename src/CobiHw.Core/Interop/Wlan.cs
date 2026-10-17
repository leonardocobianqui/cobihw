using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

internal static unsafe partial class Wlan
{
    public const uint ClientVersion = 2;
    public const int OpcodeCurrentConnection = 7;
    public const int StateConnected = 1;
    public const int BssTypeInfrastructure = 1;
    public const uint ErrorAccessDenied = 5;
    public const int BssListHeaderSize = 8;

    [StructLayout(LayoutKind.Sequential)]
    public struct Dot11Ssid
    {
        public uint Length;
        public fixed byte Bytes[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AssociationAttributes
    {
        public Dot11Ssid Ssid;
        public int BssType;
        public fixed byte Bssid[6];
        public int PhyType;
        public uint PhyIndex;
        public uint SignalQuality;
        public uint RxRateKbps;
        public uint TxRateKbps;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SecurityAttributes
    {
        public int SecurityEnabled;
        public int OneXEnabled;
        public int AuthAlgorithm;
        public int CipherAlgorithm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ConnectionAttributes
    {
        public int State;
        public int ConnectionMode;
        public fixed char ProfileName[256];
        public AssociationAttributes Association;
        public SecurityAttributes Security;
    }

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

    [LibraryImport("wlanapi.dll")]
    public static partial uint WlanOpenHandle(uint clientVersion, nint reserved, out uint negotiatedVersion, out nint clientHandle);

    [LibraryImport("wlanapi.dll")]
    public static partial uint WlanCloseHandle(nint clientHandle, nint reserved);

    [LibraryImport("wlanapi.dll")]
    public static partial uint WlanQueryInterface(nint clientHandle, in Guid interfaceGuid, int opCode, nint reserved, out uint dataSize, out nint data, out int opcodeValueType);

    [LibraryImport("wlanapi.dll")]
    public static partial uint WlanGetNetworkBssList(nint clientHandle, in Guid interfaceGuid, Dot11Ssid* ssid, int bssType, int securityEnabled, nint reserved, out nint bssList);

    [LibraryImport("wlanapi.dll")]
    public static partial void WlanFreeMemory(nint memory);
}
