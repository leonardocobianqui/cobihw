using CobiHw.Core.Readers;

namespace CobiHw.Core;

public sealed class HardwareMonitor : IDisposable
{
    private readonly Lock _lock = new();
    private readonly PdhQuery _query;
    private readonly CpuReader _cpu;
    private readonly GpuReader _gpu;
    private readonly DiskReader _disk;
    private readonly NetworkReader _network;

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
                _network.Read());
        }
    }

    public void ResetNetworkSession(Guid? device = null)
    {
        lock (_lock)
        {
            _network.ResetSession(device);
        }
    }

    public NetworkSessionState ExportNetworkSession()
    {
        lock (_lock)
        {
            return _network.ExportSession();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _query.Dispose();
        }
    }
}
