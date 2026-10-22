using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

/// <summary>
/// gdi32.dll D3DKMT declarations: the kernel graphics interface Task Manager uses for GPU temperature.
/// </summary>
internal static partial class D3dkmt
{
    /// <summary>KMTQAITYPE_ADAPTERPERFDATA, counted from d3dkmthk.h (the online list skips one entry and suggests 61).</summary>
    public const int QueryAdapterPerfData = 62;

    /// <summary>Mirrors D3DKMT_OPENADAPTERFROMLUID; <see cref="Adapter"/> is filled on success.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct OpenAdapterFromLuid
    {
        public uint LuidLowPart;
        public int LuidHighPart;
        public uint Adapter;
    }

    /// <summary>Mirrors D3DKMT_CLOSEADAPTER.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CloseAdapter
    {
        public uint Adapter;
    }

    /// <summary>Mirrors D3DKMT_QUERYADAPTERINFO; points to the structure that receives the answer.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct QueryAdapterInfo
    {
        public uint Adapter;
        public int Type;
        public nint PrivateDriverData;
        public uint PrivateDriverDataSize;
    }

    /// <summary>Mirrors D3DKMT_ADAPTER_PERFDATA (Windows 10 2004+). Zero means the driver doesn't report that value.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AdapterPerfData
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency;
        public ulong MaxMemoryFrequency;
        public ulong MaxMemoryFrequencyOC;
        public ulong MemoryBandwidth;
        public ulong PCIEBandwidth;
        public uint FanRpm;
        /// <summary>Tenths of a percent of the power limit.</summary>
        public uint Power;
        /// <summary>Tenths of a degree Celsius.</summary>
        public uint Temperature;
        public byte PowerStateOverride;
    }

    /// <summary>Opens an adapter handle; returns an NTSTATUS (0 = success).</summary>
    [LibraryImport("gdi32.dll", EntryPoint = "D3DKMTOpenAdapterFromLuid")]
    public static partial int OpenAdapter(ref OpenAdapterFromLuid open);

    /// <summary>Queries adapter information; returns an NTSTATUS.</summary>
    [LibraryImport("gdi32.dll", EntryPoint = "D3DKMTQueryAdapterInfo")]
    public static partial int QueryInfo(ref QueryAdapterInfo query);

    /// <summary>Closes an adapter handle.</summary>
    [LibraryImport("gdi32.dll", EntryPoint = "D3DKMTCloseAdapter")]
    public static partial int Close(ref CloseAdapter close);
}
