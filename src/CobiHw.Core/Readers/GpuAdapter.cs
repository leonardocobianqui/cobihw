using System.Runtime.InteropServices;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

internal sealed record GpuAdapter(string Name, ulong DedicatedMemoryBytes, string LuidTag)
{
    public static GpuAdapter? FindMain()
    {
        if (Dxgi.CreateDXGIFactory1(Dxgi.IidFactory1, out nint factoryPointer) < 0)
        {
            return null;
        }

        var factory = (Dxgi.IFactory1)Marshal.GetObjectForIUnknown(factoryPointer);
        Marshal.Release(factoryPointer);

        try
        {
            return EnumerateHardwareAdapters(factory)
                .OrderByDescending(adapter => adapter.DedicatedMemoryBytes)
                .FirstOrDefault();
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    private static List<GpuAdapter> EnumerateHardwareAdapters(Dxgi.IFactory1 factory)
    {
        var adapters = new List<GpuAdapter>();

        for (uint index = 0; ; index++)
        {
            if (factory.EnumAdapters1(index, out Dxgi.IAdapter1? adapter) < 0 || adapter is null)
            {
                break;
            }

            try
            {
                if (adapter.GetDesc1(out Dxgi.AdapterDesc1 desc) >= 0 && (desc.Flags & Dxgi.AdapterFlagSoftware) == 0)
                {
                    adapters.Add(new GpuAdapter(
                        desc.Description.Trim(),
                        desc.DedicatedVideoMemory,
                        $"luid_0x{desc.AdapterLuidHighPart:X8}_0x{desc.AdapterLuidLowPart:X8}"));
                }
            }
            finally
            {
                Marshal.ReleaseComObject(adapter);
            }
        }

        return adapters;
    }
}
