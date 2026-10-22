using System.Runtime.InteropServices;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

/// <summary>
/// Raw data about one hardware adapter, as read from DXGI before ranking.
/// </summary>
/// <param name="Name">Adapter name reported by the driver.</param>
/// <param name="VendorId">PCI vendor id (e.g. 0x10DE for NVIDIA).</param>
/// <param name="DeviceId">PCI device id (the model).</param>
/// <param name="LuidTag">LUID in the "luid_0xHIGH_0xLOW" form used by the GPU performance counters.</param>
/// <param name="DedicatedMemoryBytes">Dedicated video memory; on integrated GPUs, the small reserved slice.</param>
/// <param name="IsIntegrated">True when Direct3D 12 reports unified memory (the GPU shares system RAM).</param>
/// <param name="LuidLowPart">Low part of the numeric LUID, used to open the temperature sensor.</param>
/// <param name="LuidHighPart">High part of the numeric LUID.</param>
internal sealed record GpuAdapterInfo(string Name, uint VendorId, uint DeviceId, string LuidTag, ulong DedicatedMemoryBytes, bool IsIntegrated, uint LuidLowPart, int LuidHighPart);

/// <summary>
/// A hardware GPU with a stable identity and its position in Windows' high-performance order.
/// </summary>
/// <param name="Id">Vendor + model + instance; stays the same across reboots, unlike the LUID.</param>
/// <param name="Name">Adapter name reported by the driver.</param>
/// <param name="LuidTag">LUID in the form used by the performance counters.</param>
/// <param name="DedicatedMemoryBytes">Dedicated video memory.</param>
/// <param name="IsIntegrated">True for GPUs that share system RAM.</param>
/// <param name="Rank">0 = the GPU Windows prefers for high performance; "Automatic" picks the lowest rank.</param>
/// <param name="LuidLowPart">Low part of the numeric LUID.</param>
/// <param name="LuidHighPart">High part of the numeric LUID.</param>
internal sealed record GpuAdapter(GpuId Id, string Name, string LuidTag, ulong DedicatedMemoryBytes, bool IsIntegrated, int Rank, uint LuidLowPart, int LuidHighPart)
{
    /// <summary>Lists the hardware GPUs and ranks them the way Windows does for high-performance apps.</summary>
    /// <param name="unifiedMemoryByLuid">Cache of the Direct3D 12 "integrated?" check, so each GPU is probed only once.</param>
    /// <param name="measuredLuids">LUIDs that have memory counters; others (e.g. the duplicate entries some drivers expose) are skipped. Null disables the filter.</param>
    public static List<GpuAdapter> Enumerate(Dictionary<string, bool?> unifiedMemoryByLuid, IReadOnlySet<string>? measuredLuids)
    {
        if (Dxgi.CreateDXGIFactory1(Dxgi.IidFactory1, out nint factoryPointer) < 0)
        {
            return [];
        }

        var factory = (Dxgi.IFactory1)Marshal.GetObjectForIUnknown(factoryPointer);
        Marshal.Release(factoryPointer);

        try
        {
            List<GpuAdapterInfo> adapters = ListHardware(factory, unifiedMemoryByLuid, measuredLuids);

            // IDXGIFactory6 (Windows 10 1803+) knows the preference order; older systems fall back to memory size.
            List<string>? highPerformanceOrder = factory is Dxgi.IFactory6 preference ? HighPerformanceOrder(preference) : null;

            return Describe(adapters, highPerformanceOrder);
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>Assigns identity and rank to each adapter. Separate from <see cref="Enumerate"/> so it can be unit tested.</summary>
    /// <param name="adapters">Adapters in DXGI enumeration order.</param>
    /// <param name="highPerformanceOrder">LUID tags in Windows' preference order, or null to rank by dedicated memory.</param>
    public static List<GpuAdapter> Describe(IReadOnlyList<GpuAdapterInfo> adapters, List<string>? highPerformanceOrder)
    {
        List<string> byDedicatedMemory = [.. adapters.OrderByDescending(adapter => adapter.DedicatedMemoryBytes).Select(adapter => adapter.LuidTag)];
        var instances = new Dictionary<(uint VendorId, uint DeviceId), int>();
        var result = new List<GpuAdapter>(adapters.Count);

        foreach (GpuAdapterInfo adapter in adapters)
        {
            // Two identical cards get instances 0 and 1, so a saved choice can tell them apart.
            int instance = instances.GetValueOrDefault((adapter.VendorId, adapter.DeviceId));
            instances[(adapter.VendorId, adapter.DeviceId)] = instance + 1;

            // Adapters missing from Windows' order go after the ones it listed.
            int fallbackRank = byDedicatedMemory.IndexOf(adapter.LuidTag);
            int preferredRank = highPerformanceOrder?.IndexOf(adapter.LuidTag) ?? -1;
            int rank = highPerformanceOrder is null ? fallbackRank : preferredRank >= 0 ? preferredRank : adapters.Count + fallbackRank;

            result.Add(new GpuAdapter(
                new GpuId(adapter.VendorId, adapter.DeviceId, instance),
                adapter.Name,
                adapter.LuidTag,
                adapter.DedicatedMemoryBytes,
                adapter.IsIntegrated,
                rank,
                adapter.LuidLowPart,
                adapter.LuidHighPart));
        }

        return result;
    }

    private static List<GpuAdapterInfo> ListHardware(Dxgi.IFactory1 factory, Dictionary<string, bool?> unifiedMemoryByLuid, IReadOnlySet<string>? measuredLuids)
    {
        var adapters = new List<GpuAdapterInfo>();

        for (uint index = 0; ; index++)
        {
            if (factory.EnumAdapters1(index, out Dxgi.IAdapter1? adapter) < 0 || adapter is null)
            {
                break;
            }

            try
            {
                // Skips the Microsoft Basic Render Driver and other software adapters.
                if (adapter.GetDesc1(out Dxgi.AdapterDesc1 desc) < 0 || (desc.Flags & Dxgi.AdapterFlagSoftware) != 0)
                {
                    continue;
                }

                string luidTag = Dxgi.LuidTag(desc);
                if (!IsMeasured(luidTag, measuredLuids))
                {
                    continue;
                }

                // Creating a D3D12 device briefly wakes the GPU, so the answer is cached per LUID.
                if (!unifiedMemoryByLuid.TryGetValue(luidTag, out bool? unifiedMemory))
                {
                    unifiedMemory = D3D12.UsesUnifiedMemory(adapter);
                    unifiedMemoryByLuid[luidTag] = unifiedMemory;
                }

                adapters.Add(new GpuAdapterInfo(desc.Description.Trim(), desc.VendorId, desc.DeviceId, luidTag, desc.DedicatedVideoMemory, unifiedMemory == true, desc.AdapterLuidLowPart, desc.AdapterLuidHighPart));
            }
            finally
            {
                Marshal.ReleaseComObject(adapter);
            }
        }

        return adapters;
    }

    /// <summary>True when the adapter has memory counters, or when the counters aren't available at all.</summary>
    public static bool IsMeasured(string luidTag, IReadOnlySet<string>? measuredLuids) =>
        measuredLuids is null || measuredLuids.Contains(luidTag);

    // EnumAdapterByGpuPreference returns adapters already sorted for "high performance".
    private static List<string> HighPerformanceOrder(Dxgi.IFactory6 factory)
    {
        var order = new List<string>();
        Guid adapterInterface = Dxgi.IidAdapter1;

        for (uint index = 0; ; index++)
        {
            if (factory.EnumAdapterByGpuPreference(index, Dxgi.GpuPreferenceHighPerformance, ref adapterInterface, out Dxgi.IAdapter1? adapter) < 0 || adapter is null)
            {
                break;
            }

            try
            {
                if (adapter.GetDesc1(out Dxgi.AdapterDesc1 desc) >= 0)
                {
                    order.Add(Dxgi.LuidTag(desc));
                }
            }
            finally
            {
                Marshal.ReleaseComObject(adapter);
            }
        }

        return order;
    }
}
