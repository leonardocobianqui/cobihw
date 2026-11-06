using CobiHw.Core.Linux;
using CobiHw.Core.Tests.Support;

namespace CobiHw.Core.Tests.Linux;

/// <summary>Everything read together, as the widget gets it.</summary>
public sealed class HardwareMonitorTests
{
    [Fact]
    public void Notebook_EveryReadingIsThere_ExceptTheGpuForNow()
    {
        using FakeSystem system = Notebook.Build();
        using var monitor = new HardwareMonitor(system.Files, null);

        HardwareSnapshot snapshot = monitor.Sample();

        Assert.Equal("AMD Ryzen 5 7520U with Radeon Graphics", snapshot.Cpu.Name);
        Assert.Equal(64.1, snapshot.Memory!.UsagePercent, 1);
        Assert.Equal("/", snapshot.Disk.System!.Volumes);
        Assert.NotNull(snapshot.Network.DownloadBytesPerSecond);
        Assert.Equal(100, snapshot.Battery!.Percent);
        Assert.Empty(snapshot.Gpu.Gpus);
    }

    [Fact]
    public void NetworkSession_ResetAndExport()
    {
        using FakeSystem system = Notebook.Build();
        using var monitor = new HardwareMonitor(system.Files, null);
        Guid wifi = InterfaceId.From("wlp2s0");

        monitor.ResetNetworkSession(wifi);
        monitor.ResetNetworkSession();
        NetworkSessionState session = monitor.ExportNetworkSession();

        Assert.Equal(0UL, session.ReceivedBytes);
        Assert.Equal(0UL, session.Devices[wifi].ReceivedBytes);
    }
}
