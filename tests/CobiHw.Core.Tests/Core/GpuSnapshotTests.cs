namespace CobiHw.Core.Tests.Core;

/// <summary>Choosing a GPU from a reading and adding up its memory.</summary>
public sealed class GpuSnapshotTests
{
    private const ulong MiB = 1024UL * 1024;
    private const ulong GiB = 1024UL * MiB;

    // Integrated: 128 MB reserved, all in use, plus 1.1 GB shared. Dedicated: first in the high-performance order.
    private static readonly GpuActivity IntelGpu = new(new GpuId(0x8086, 0x5917, 0), "Intel(R) UHD Graphics 620", 1, true, 12, 128 * MiB, 128 * MiB, 1100 * MiB);
    private static readonly GpuActivity NvidiaGpu = new(new GpuId(0x10DE, 0x2520, 0), "NVIDIA GeForce RTX 3060 Laptop GPU", 0, false, 40, 2355 * MiB, 6 * GiB, null);

    [Fact]
    public void Preferred_IsTheBestRanked()
    {
        var gpus = new GpuSnapshot([IntelGpu, NvidiaGpu]);

        Assert.Equal(NvidiaGpu, gpus.Preferred);
        Assert.Equal(IntelGpu, gpus.Find(IntelGpu.Id));
        Assert.Null(new GpuSnapshot([]).Preferred);
    }

    [Fact]
    public void TotalUsedBytes_IntegratedAddsReservedAndShared()
    {
        Assert.Equal(1228 * MiB, IntelGpu.TotalUsedBytes);
        Assert.Equal(2355 * MiB, NvidiaGpu.TotalUsedBytes);
        Assert.Null((IntelGpu with { DedicatedUsedBytes = null, SharedUsedBytes = null }).TotalUsedBytes);
    }
}
