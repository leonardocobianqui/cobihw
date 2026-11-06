using System.Diagnostics;
using CobiHw.Core.Linux.Readers;

namespace CobiHw.Core.Linux;

/// <summary>
/// Linux implementation of <see cref="IHardwareMonitor"/>: reads /proc and /sys and returns one <see cref="HardwareSnapshot"/> per call.
/// </summary>
/// <remarks>Thread-safe: sampling, session reset and export are serialized by a lock. GPUs aren't read yet.</remarks>
public sealed class HardwareMonitor : IHardwareMonitor
{
    private readonly Lock _lock = new();
    private readonly SystemFiles _files;
    private readonly CpuReader _cpu;
    private readonly DiskReader _disk;
    private readonly NetworkReader _network;

    /// <summary>Creates the readers and takes a first sample, so the next one already has speeds and rates.</summary>
    /// <param name="savedNetworkSession">Session saved by a previous run, or <see langword="null"/> to start fresh.</param>
    public HardwareMonitor(NetworkSessionState? savedNetworkSession = null)
        : this(SystemFiles.Live, savedNetworkSession)
    {
    }

    /// <summary>Monitor over a test folder that mimics /proc and /sys.</summary>
    internal HardwareMonitor(SystemFiles files, NetworkSessionState? savedNetworkSession)
    {
        _files = files;
        _cpu = new CpuReader(files);
        _disk = new DiskReader(files);
        _network = new NetworkReader(files, savedNetworkSession);

        long timestamp = Stopwatch.GetTimestamp();
        _cpu.Read();
        _disk.Read(timestamp);
        _network.Read(timestamp);
    }

    /// <inheritdoc/>
    public HardwareSnapshot Sample()
    {
        lock (_lock)
        {
            long timestamp = Stopwatch.GetTimestamp();
            return new HardwareSnapshot(
                _cpu.Read(),
                MemoryReader.Read(_files),
                new GpuSnapshot([]),
                _disk.Read(timestamp),
                _network.Read(timestamp),
                BatteryReader.Read(_files));
        }
    }

    /// <inheritdoc/>
    public void ResetNetworkSession(Guid? device = null)
    {
        lock (_lock)
        {
            _network.ResetSession(device);
        }
    }

    /// <inheritdoc/>
    public NetworkSessionState ExportNetworkSession()
    {
        lock (_lock)
        {
            return _network.ExportSession();
        }
    }

    /// <summary>Nothing to release: every read opens and closes its own file.</summary>
    public void Dispose()
    {
    }
}
