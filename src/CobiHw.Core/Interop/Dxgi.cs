using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

/// <summary>
/// DXGI declarations used to list GPUs and Windows' high-performance preference order.
/// </summary>
/// <remarks>COM interfaces declare only the methods up to the one we call; the stubs keep the vtable order.</remarks>
internal static partial class Dxgi
{
    /// <summary>DXGI_ADAPTER_FLAG_SOFTWARE (e.g. Microsoft Basic Render Driver).</summary>
    public const uint AdapterFlagSoftware = 0x2;
    /// <summary>DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE.</summary>
    public const int GpuPreferenceHighPerformance = 2;

    public static readonly Guid IidFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    public static readonly Guid IidAdapter1 = new("29038f61-3839-4626-91fd-086879011a05");

    /// <summary>Mirrors DXGI_ADAPTER_DESC1.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct AdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public uint AdapterLuidLowPart;
        public int AdapterLuidHighPart;
        public uint Flags;
    }

    /// <summary>IDXGIFactory1, declared up to EnumAdapters1.</summary>
    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFactory1
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();
        void EnumAdapters();
        void MakeWindowAssociation();
        void GetWindowAssociation();
        void CreateSwapChain();
        void CreateSoftwareAdapter();

        [PreserveSig]
        int EnumAdapters1(uint index, out IAdapter1? adapter);
    }

    /// <summary>IDXGIFactory6 (Windows 10 1803+), declared up to EnumAdapterByGpuPreference.</summary>
    [ComImport]
    [Guid("c1b6694f-ff09-44a9-b03c-77900a0a1d17")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFactory6
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();
        void EnumAdapters();
        void MakeWindowAssociation();
        void GetWindowAssociation();
        void CreateSwapChain();
        void CreateSoftwareAdapter();
        void EnumAdapters1();
        void IsCurrent();
        void IsWindowedStereoEnabled();
        void CreateSwapChainForHwnd();
        void CreateSwapChainForCoreWindow();
        void GetSharedResourceAdapterLuid();
        void RegisterStereoStatusWindow();
        void RegisterStereoStatusEvent();
        void UnregisterStereoStatus();
        void RegisterOcclusionStatusWindow();
        void RegisterOcclusionStatusEvent();
        void UnregisterOcclusionStatus();
        void CreateSwapChainForComposition();
        void GetCreationFlags();
        void EnumAdapterByLuid();
        void EnumWarpAdapter();
        void CheckFeatureSupport();

        [PreserveSig]
        int EnumAdapterByGpuPreference(uint index, int preference, ref Guid riid, out IAdapter1? adapter);
    }

    /// <summary>IDXGIAdapter1, declared up to GetDesc1.</summary>
    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAdapter1
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();
        void EnumOutputs();
        void GetDesc();
        void CheckInterfaceSupport();

        [PreserveSig]
        int GetDesc1(out AdapterDesc1 desc);
    }

    /// <summary>Creates the DXGI factory; returns an HRESULT.</summary>
    [LibraryImport("dxgi.dll")]
    public static partial int CreateDXGIFactory1(in Guid riid, out nint factory);

    /// <summary>Formats the LUID the way GPU performance counters name it: "luid_0xHIGH_0xLOW".</summary>
    public static string LuidTag(in AdapterDesc1 desc) =>
        $"luid_0x{desc.AdapterLuidHighPart:X8}_0x{desc.AdapterLuidLowPart:X8}";
}
