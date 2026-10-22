using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

/// <summary>
/// Reads one GPU's temperature through the D3DKMT driver interface, the same source Task Manager uses.
/// </summary>
/// <remarks>The adapter handle stays open until <see cref="Dispose"/>, so each read is a single driver call.</remarks>
internal sealed class GpuSensor : IDisposable
{
    private readonly uint _adapter;
    private bool _closed;

    private GpuSensor(uint adapter) => _adapter = adapter;

    /// <summary>Opens the adapter identified by its LUID (the same id DXGI reports).</summary>
    /// <returns>The sensor, or <see langword="null"/> if Windows refuses to open the adapter.</returns>
    public static GpuSensor? Open(uint luidLowPart, int luidHighPart)
    {
        var open = new D3dkmt.OpenAdapterFromLuid { LuidLowPart = luidLowPart, LuidHighPart = luidHighPart };
        return D3dkmt.OpenAdapter(ref open) == 0 ? new GpuSensor(open.Adapter) : null;
    }

    /// <summary>Reads the current temperature.</summary>
    /// <returns>Degrees Celsius, or <see langword="null"/> when the driver doesn't report it (common on integrated GPUs and VMs).</returns>
    public unsafe double? ReadTemperature()
    {
        var data = new D3dkmt.AdapterPerfData();
        var query = new D3dkmt.QueryAdapterInfo
        {
            Adapter = _adapter,
            Type = D3dkmt.QueryAdapterPerfData,
            PrivateDriverData = (nint)(&data),
            PrivateDriverDataSize = (uint)sizeof(D3dkmt.AdapterPerfData),
        };

        return D3dkmt.QueryInfo(ref query) == 0 ? TemperatureOf(data) : null;
    }

    /// <summary>Converts the driver's tenths of a degree to °C; zero means "not reported".</summary>
    internal static double? TemperatureOf(in D3dkmt.AdapterPerfData data) =>
        data.Temperature == 0 ? null : data.Temperature / 10.0;

    /// <summary>Closes the adapter handle. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        var close = new D3dkmt.CloseAdapter { Adapter = _adapter };
        D3dkmt.Close(ref close);
    }
}
