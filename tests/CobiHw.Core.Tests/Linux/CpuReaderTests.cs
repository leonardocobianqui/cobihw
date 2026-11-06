using CobiHw.Core.Linux.Readers;
using CobiHw.Core.Tests.Support;

namespace CobiHw.Core.Tests.Linux;

/// <summary>Processor usage from /proc/stat, clock from cpufreq and name from /proc/cpuinfo.</summary>
public sealed class CpuReaderTests
{
    [Fact]
    public void Times_IdleIncludesIoWait_AndGuestTimeIsNotCountedTwice()
    {
        // user nice system idle iowait irq softirq steal guest guest_nice
        CpuTimes times = CpuReader.ParseTimes("cpu  100 0 50 800 40 0 10 0 30 0\ncpu0 1 2 3 4 5 6 7 8 9 10")!.Value;

        Assert.Equal(1000, times.Total);
        Assert.Equal(160, times.Busy);
    }

    [Fact]
    public void Usage_IsTheBusyShareOfTheInterval()
    {
        Assert.Equal(25, CpuReader.Usage(new CpuTimes(100, 1000), new CpuTimes(150, 1200))!.Value, 6);
        // No time passed: nothing to measure.
        Assert.Null(CpuReader.Usage(new CpuTimes(100, 1000), new CpuTimes(100, 1000)));
        // iowait can go backwards; the result stays within 0–100.
        Assert.Equal(0, CpuReader.Usage(new CpuTimes(100, 1000), new CpuTimes(90, 1100))!.Value);
    }

    [Fact]
    public void Notebook_NameFromCpuInfo_ClockIsTheAverageOfTheCores_FirstReadHasNoUsage()
    {
        using FakeSystem system = Notebook.Build();
        var reader = new CpuReader(system.Files);

        CpuSnapshot first = reader.Read();
        system.File("proc/stat", "cpu  406984 3182 136780 9306883 81958 0 16471 0 6781 0\nbtime 1793993870\n");
        CpuSnapshot second = reader.Read();

        Assert.Equal("AMD Ryzen 5 7520U with Radeon Graphics", first.Name);
        Assert.Equal((4292015 + 4290736 + 1109831 + 4291771) / 4.0 / 1_000_000, first.FrequencyGhz!.Value, 6);
        Assert.Null(first.UsagePercent);
        // 2000 busy ticks out of 3000.
        Assert.Equal(200.0 / 3, second.UsagePercent!.Value, 6);
    }

    [Fact]
    public void WithoutCpuFreq_ClockFallsBackToCpuInfo_AndWithoutEither_ItIsUnknown()
    {
        using FakeSystem withCpuInfo = new FakeSystem().File("proc/cpuinfo", Notebook.CpuInfo);
        using FakeSystem withNothing = new FakeSystem().File("proc/cpuinfo", "processor\t: 0\nmodel name\t: QEMU Virtual CPU\n");

        Assert.Equal((4291.991 + 3086.202) / 2 / 1000, new CpuReader(withCpuInfo.Files).Read().FrequencyGhz!.Value, 6);
        Assert.Null(new CpuReader(withNothing.Files).Read().FrequencyGhz);
    }

    [Fact]
    public void Arm_WithoutModelName_UsesTheDeviceTreeModel()
    {
        using FakeSystem system = new FakeSystem()
            .File("proc/cpuinfo", "processor\t: 0\nBogoMIPS\t: 108.00\nCPU implementer\t: 0x41\n")
            .File("sys/firmware/devicetree/base/model", "Raspberry Pi 5 Model B Rev 1.0\0");

        Assert.Equal("Raspberry Pi 5 Model B Rev 1.0", CpuReader.ReadName(system.Files));
    }
}
