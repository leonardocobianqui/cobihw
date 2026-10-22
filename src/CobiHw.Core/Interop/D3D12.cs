using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

/// <summary>
/// Asks Direct3D 12 whether a GPU uses unified memory, which is how integrated GPUs are detected.
/// </summary>
internal static unsafe partial class D3D12
{
    private const int FeatureLevel11 = 0xb000;
    private const int FeatureArchitecture = 1;
    // ID3D12Device::CheckFeatureSupport position in the vtable (IUnknown 3 + ID3D12Object 4 + 6 device methods).
    private const int CheckFeatureSupportSlot = 13;

    private static readonly Guid IidDevice = new("189819f1-1db6-4b57-be54-1821339b85f7");

    // Mirrors D3D12_FEATURE_DATA_ARCHITECTURE.
    [StructLayout(LayoutKind.Sequential)]
    private struct FeatureDataArchitecture
    {
        public uint NodeIndex;
        public int TileBasedRenderer;
        public int Uma;
        public int CacheCoherentUma;
    }

    [LibraryImport("d3d12.dll")]
    private static partial int D3D12CreateDevice(nint adapter, int minimumFeatureLevel, in Guid riid, out nint device);

    /// <summary>Creates a throwaway D3D12 device on the adapter and reads its architecture.</summary>
    /// <param name="adapter">A DXGI adapter COM object.</param>
    /// <returns>True for unified memory (integrated GPU), false for dedicated, null without DirectX 12 support.</returns>
    /// <remarks>Briefly wakes the GPU, so callers cache the result.</remarks>
    public static bool? UsesUnifiedMemory(object adapter)
    {
        nint unknown = Marshal.GetIUnknownForObject(adapter);
        nint device = 0;

        try
        {
            if (D3D12CreateDevice(unknown, FeatureLevel11, IidDevice, out device) < 0)
            {
                return null;
            }

            var data = new FeatureDataArchitecture();
            // Called through the vtable to avoid declaring the whole ID3D12Device interface.
            var checkFeatureSupport = (delegate* unmanaged[Stdcall]<nint, int, FeatureDataArchitecture*, uint, int>)(*(nint**)device)[CheckFeatureSupportSlot];

            return checkFeatureSupport(device, FeatureArchitecture, &data, (uint)sizeof(FeatureDataArchitecture)) < 0 ? null : data.Uma != 0;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            if (device != 0)
            {
                Marshal.Release(device);
            }

            Marshal.Release(unknown);
        }
    }
}
