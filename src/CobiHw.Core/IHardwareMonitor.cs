namespace CobiHw.Core;

/// <summary>
/// Reads every sensor of the machine, one <see cref="HardwareSnapshot"/> per call. Each platform has its own implementation.
/// </summary>
/// <remarks>Implementations are thread-safe and take a first sample when created, so the first call already has speeds and rates.</remarks>
public interface IHardwareMonitor : IDisposable
{
    /// <summary>Samples every reader. Call about once a second: rates are averaged since the previous call.</summary>
    HardwareSnapshot Sample();

    /// <summary>Zeroes the session totals of one network device, or the widget total when <paramref name="device"/> is null.</summary>
    void ResetNetworkSession(Guid? device = null);

    /// <summary>Copy of the network session to save on exit.</summary>
    NetworkSessionState ExportNetworkSession();
}
