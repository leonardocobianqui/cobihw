using CobiHw.Core;
using static CobiHw.Tests.TestData;

namespace CobiHw.Tests.Core;

/// <summary>Choosing a GPU from a reading and adding up its memory.</summary>
public sealed class GpuSnapshotTests
{
    private const ulong MiB = 1024UL * 1024;

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
