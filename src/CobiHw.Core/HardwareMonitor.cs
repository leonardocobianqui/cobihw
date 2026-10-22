using CobiHw.Core.Readers;

namespace CobiHw.Core;

/// <summary>
/// Entry point of the Core library: owns the readers and returns one <see cref="HardwareSnapshot"/> per call.
/// </summary>
/// <remarks>Thread-safe: sampling, session reset and export are serialized by a lock.</remarks>
public sealed class HardwareMonitor : IDisposable
{
    private readonly Lock _lock = new();
    private readonly PdhQuery _query;
    private readonly CpuReader _cpu;
    private readonly GpuReader _gpu;
    private readonly DiskReader _disk;
    private readonly NetworkReader _network;

    /// <summary>Creates the readers and takes a first sample, so the next one already has speeds and rates.</summary>
    /// <param name="savedNetworkSession">Session saved by a previous run, or <see langword="null"/> to start fresh.</param>
    public HardwareMonitor(NetworkSessionState? savedNetworkSession = null)
    {
        _query = new PdhQuery();
        _cpu = new CpuReader(_query);
        _gpu = new GpuReader(_query);
        _disk = new DiskReader(_query);
        _network = new NetworkReader(savedNetworkSession);

        _query.Collect();
        _network.Read();
    }

    /// <summary>Samples every reader. Call about once a second: rates are averaged since the previous call.</summary>
    public HardwareSnapshot Sample()
    {
        lock (_lock)
        {
            _query.Collect();

            return new HardwareSnapshot(
                _cpu.Read(),
                MemoryReader.Read(),
                _gpu.Read(),
                _disk.Read(),
                _network.Read(),
                BatteryReader.Read());
        }
    }

    /// <summary>Zeroes the session totals of one network device, or the widget total when <paramref name="device"/> is null.</summary>
    public void ResetNetworkSession(Guid? device = null)
    {
        lock (_lock)
        {
            _network.ResetSession(device);
        }
    }

    /// <summary>Copy of the network session to save on exit.</summary>
    public NetworkSessionState ExportNetworkSession()
    {
        lock (_lock)
        {
            return _network.ExportSession();
        }
    }

    /// <summary>Releases the PDH query and the GPU sensors.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _gpu.Dispose();
            _query.Dispose();
        }
    }
}
