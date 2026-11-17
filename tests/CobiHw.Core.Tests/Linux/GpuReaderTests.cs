using System.Diagnostics;
using CobiHw.Core.Linux.Readers;
using CobiHw.Core.Tests.Support;

namespace CobiHw.Core.Tests.Linux;

/// <summary>GPUs from /sys/class/drm, named from pci.ids, with usage from sysfs or from the processes' fdinfo.</summary>
public sealed class GpuReaderTests
{
    private const long Start = 1_000;
    private const ulong MiB = 1024UL * 1024;

    private static long Seconds(double seconds) => Start + (long)(seconds * Stopwatch.Frequency);

    [Fact]
    public void Notebook_TheIntegratedRadeon_FromSysfs()
    {
        using FakeSystem system = Notebook.Build();

        GpuActivity gpu = Assert.Single(new GpuReader(system.Files).Read(Start).Gpus);

        Assert.Equal(new GpuId(0x1002, 0x1506, 0), gpu.Id);
        Assert.Equal("AMD Mendocino", gpu.Name);
        Assert.Equal(0, gpu.Rank);
        Assert.True(gpu.IsIntegrated);
        Assert.Equal(0, gpu.UsagePercent!.Value, 6);
        Assert.Equal(504061952UL, gpu.DedicatedUsedBytes);
        Assert.Equal(512 * MiB, gpu.DedicatedTotalBytes);
        Assert.Equal(813862912UL, gpu.SharedUsedBytes);
        Assert.Equal(57, gpu.TemperatureCelsius!.Value, 6);
    }

    [Fact]
    public void Amd_WithVramChips_IsDedicated_AndHasNoSharedMemory()
    {
        using FakeSystem system = Notebook.Build().File("sys/class/drm/card1/device/mem_info_vram_vendor", "samsung\n");

        GpuActivity gpu = new GpuReader(system.Files).Read(Start).Gpus[0];

        Assert.False(gpu.IsIntegrated);
        Assert.Null(gpu.SharedUsedBytes);
    }

    [Fact]
    public void SuspendedGpu_IsIdle_AndItsPowerAttributesAreNotRead()
    {
        using FakeSystem system = Notebook.Build().File("sys/class/drm/card1/device/power/runtime_status", "suspended\n");

        GpuActivity gpu = new GpuReader(system.Files).Read(Start).Gpus[0];

        Assert.Equal(0, gpu.UsagePercent!.Value, 6);
        Assert.Null(gpu.DedicatedUsedBytes);
        Assert.Null(gpu.TemperatureCelsius);
        Assert.Equal("AMD Mendocino", gpu.Name);
    }

    [Fact]
    public void Laptop_WithIntelAndNvidia_TheDedicatedGpuComesFirst_AndEachKeepsItsName()
    {
        using FakeSystem system = new FakeSystem().File("usr/share/misc/pci.ids", Notebook.PciIds);
        Card(system, "card0", 0x8086, 0x46a6, "0000:00:02.0", "i915");
        Card(system, "card1", 0x10de, 0x2520, "0000:01:00.0", "nvidia");

        IReadOnlyList<GpuActivity> gpus = new GpuReader(system.Files).Read(Start).Gpus;

        Assert.Equal(new[] { "Intel Iris Xe Graphics", "NVIDIA GeForce RTX 3060 Mobile / Max-Q" }, gpus.Select(gpu => gpu.Name));
        Assert.Equal(new[] { true, false }, gpus.Select(gpu => gpu.IsIntegrated));
        Assert.Equal(new[] { 1, 0 }, gpus.Select(gpu => gpu.Rank));
        Assert.Equal("NVIDIA GeForce RTX 3060 Mobile / Max-Q", new GpuSnapshot(gpus).Preferred!.Name);

        // NVIDIA's proprietary driver has no usage in sysfs or fdinfo; it's read through NVML in a later change.
        Assert.Null(gpus[1].UsagePercent);
    }

    [Fact]
    public void TwoIdenticalCards_AreNumberedInPciOrder()
    {
        using FakeSystem system = new FakeSystem();
        Card(system, "card2", 0x1002, 0x73bf, "0000:0a:00.0", "amdgpu");
        Card(system, "card1", 0x1002, 0x73bf, "0000:03:00.0", "amdgpu");

        IReadOnlyList<GpuActivity> gpus = new GpuReader(system.Files).Read(Start).Gpus;

        Assert.Equal(new[] { 0, 1 }, gpus.Select(gpu => gpu.Id.Instance));
        // No pci.ids: vendor and device id.
        Assert.Equal("AMD GPU 73BF", gpus[0].Name);
    }

    [Fact]
    public void Intel_UsageFromTheBusiestEngine_CountingEachClientOnce()
    {
        using FakeSystem system = new FakeSystem();
        Card(system, "card0", 0x8086, 0x46a6, "0000:00:02.0", "i915");
        // The desktop holds the GPU through two descriptors of the same client; the browser is a second client.
        Client(system, 1200, 5, desktop: 0, video: 0);
        Client(system, 1200, 9, desktop: 0, video: 0);
        Client(system, 3400, 7, desktop: 0, video: 0, clientId: "2");
        var reader = new GpuReader(system.Files);
        Assert.Null(reader.Read(Start).Gpus[0].UsagePercent);

        // Over 2 s: the desktop renders 0.5 s, the browser 0.3 s, and the video engine decodes 1.2 s.
        Client(system, 1200, 5, desktop: 500_000_000, video: 0);
        Client(system, 1200, 9, desktop: 500_000_000, video: 0);
        Client(system, 3400, 7, desktop: 300_000_000, video: 1_200_000_000, clientId: "2");
        GpuActivity gpu = reader.Read(Seconds(2)).Gpus[0];

        Assert.Equal(60, gpu.UsagePercent!.Value, 6);
        Assert.Equal(2 * 64 * MiB, gpu.SharedUsedBytes);
        Assert.Null(gpu.DedicatedUsedBytes);
    }

    [Fact]
    public void FdInfo_EnginesSharedBySeveralUnits_AreScaledToOne()
    {
        (string Device, string Client, DrmClient Statistics) client = DrmClients.Parse(
            "drm-pdev:\t0000:00:02.0\ndrm-client-id:\t3\ndrm-engine-video:\t4000 ns\ndrm-engine-capacity-video:\t2\ndrm-resident-local0:\t2 MiB\n")!.Value;

        Assert.Equal(2000, client.Statistics.EngineNanoseconds["video"]);
        Assert.Equal(2 * MiB, client.Statistics.LocalBytes);
        Assert.Null(DrmClients.Parse("pos:\t0\nflags:\t02\n"));
    }

    [Fact]
    public void FdInfo_RealAmdClient_VramIsLocal_GttIsSystemRam()
    {
        (string Device, string Client, DrmClient Statistics) client = DrmClients.Parse(Notebook.GpuClient)!.Value;

        Assert.Equal("0000:03:00.0", client.Device);
        Assert.Equal("7", client.Client);
        Assert.Equal(302992942785, client.Statistics.EngineNanoseconds["gfx"]);
        Assert.Equal(687136348, client.Statistics.EngineNanoseconds["compute"]);
        Assert.Equal(322236UL * 1024, client.Statistics.LocalBytes);
        Assert.Equal(412936UL * 1024, client.Statistics.SystemBytes);
    }

    [Fact]
    public void Names_UseTheMarketingNameInBrackets_AndSkipSubsystemsAndOtherVendors()
    {
        Dictionary<(uint, uint), string> names = PciNames.Parse(Notebook.PciIds, [(0x1002, 0x1505), (0x1002, 0x1506), (0x1022, 0x1506)]);

        Assert.Equal("AMD Radeon 610M", names[(0x1002, 0x1505)]);
        Assert.Equal("AMD Mendocino", names[(0x1002, 0x1506)]);
        // Same device id under another vendor: named from its own entry, with the bracketed short name.
        Assert.Equal("AMD Not a GPU", names[(0x1022, 0x1506)]);
    }

    private static void Card(FakeSystem system, string card, uint vendor, uint device, string slot, string driver)
    {
        string folder = $"sys/class/drm/{card}/device";
        system.File($"{folder}/vendor", $"0x{vendor:x4}\n")
            .File($"{folder}/device", $"0x{device:x4}\n")
            .File($"{folder}/class", "0x030000\n")
            .File($"{folder}/uevent", $"DRIVER={driver}\nPCI_SLOT_NAME={slot}\n");
    }

    private static void Client(FakeSystem system, int process, int descriptor, long desktop, long video, string clientId = "1") =>
        system.File(
            $"proc/{process}/fdinfo/{descriptor}",
            $"pos:\t0\ndrm-driver:\ti915\ndrm-client-id:\t{clientId}\ndrm-pdev:\t0000:00:02.0\ndrm-resident-system0:\t64 MiB\n" +
            $"drm-engine-render:\t{desktop} ns\ndrm-engine-video:\t{video} ns\n");
}
