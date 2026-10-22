using System.Runtime.InteropServices;
using CobiHw.Core;
using CobiHw.Core.Interop;
using CobiHw.Core.Readers;

namespace CobiHw.Tests.Core;

/// <summary>GPU identity, ranking, performance counters and the temperature sensor.</summary>
[Collection(AppState.Name)]
public sealed class GpuTests
{
    private const ulong MiB = 1024UL * 1024;
    private const ulong GiB = 1024UL * MiB;
    private const string IntelLuid = "luid_0x00000000_0x0000A001";
    private const string NvidiaLuid = "luid_0x00000000_0x0000B002";

    private static readonly GpuAdapterInfo IntelInfo = new("Intel(R) UHD Graphics 620", 0x8086, 0x5917, IntelLuid, 128 * MiB, true, 0xA001, 0);
    private static readonly GpuAdapterInfo NvidiaInfo = new("NVIDIA GeForce RTX 3060 Laptop GPU", 0x10DE, 0x2520, NvidiaLuid, 6 * GiB, false, 0xB002, 0);

    internal static readonly GpuActivity Intel = new(new GpuId(0x8086, 0x5917, 0), "Intel(R) UHD Graphics 620", 1, true, 12, 128 * MiB, 128 * MiB, 1100 * MiB);
    internal static readonly GpuActivity Nvidia = new(new GpuId(0x10DE, 0x2520, 0), "NVIDIA GeForce RTX 3060 Laptop GPU", 0, false, 40, 2355 * MiB, 6 * GiB, null);

    [Fact]
    public void Describe_RanksByWindowsHighPerformanceOrder()
    {
        List<GpuAdapter> described = GpuAdapter.Describe([IntelInfo, NvidiaInfo], [NvidiaLuid, IntelLuid]);

        Assert.Equal(0, described.Single(adapter => adapter.Name.StartsWith("NVIDIA", StringComparison.Ordinal)).Rank);
        Assert.Equal(1, described.Single(adapter => adapter.Name.StartsWith("Intel", StringComparison.Ordinal)).Rank);
        Assert.Equal(new GpuId(0x8086, 0x5917, 0), described[0].Id);
        Assert.True(described[0].IsIntegrated);
        Assert.False(described[1].IsIntegrated);
    }

    [Fact]
    public void Describe_WithoutWindowsOrder_LargestDedicatedMemoryFirst()
    {
        List<GpuAdapter> described = GpuAdapter.Describe([IntelInfo, NvidiaInfo], null);

        Assert.Equal(0, described[1].Rank);
        Assert.Equal(1, described[0].Rank);
    }

    [Fact]
    public void Describe_AdapterMissingFromWindowsOrder_GoesLast()
    {
        List<GpuAdapter> described = GpuAdapter.Describe([IntelInfo, NvidiaInfo], [IntelLuid]);

        Assert.Equal(0, described[0].Rank);
        Assert.True(described[1].Rank > described[0].Rank);
    }

    [Fact]
    public void Describe_TwoIdenticalCards_GetInstancesZeroAndOne()
    {
        List<GpuAdapter> twins = GpuAdapter.Describe([NvidiaInfo, NvidiaInfo with { LuidTag = "luid_0x00000000_0x0000C003" }], null);

        Assert.Equal(0, twins[0].Id.Instance);
        Assert.Equal(1, twins[1].Id.Instance);
        Assert.NotEqual(twins[0].Id, twins[1].Id);
    }

    [Fact]
    public void Describe_KeepsTheNumericLuid_ToOpenTheSensor()
    {
        GpuAdapter adapter = GpuAdapter.Describe([NvidiaInfo with { LuidHighPart = 1 }], null)[0];

        Assert.Equal(0xB002u, adapter.LuidLowPart);
        Assert.Equal(1, adapter.LuidHighPart);
    }

    [Theory]
    [InlineData("pid_1234_luid_0x00000000_0x0000B002_phys_0_eng_0_engtype_3D", NvidiaLuid)]
    [InlineData("luid_0x00000000_0x0000A001_phys_0", IntelLuid)]
    [InlineData("_Total", null)]
    public void LuidOf_ReadsTheLuidFromTheCounterName(string instance, string? expected) =>
        Assert.Equal(expected, GpuReader.LuidOf(instance));

    [Fact]
    public void UsagePercent_SumsProcessesPerEngine_AndTakesTheBusiestEngine()
    {
        (string, double)[] engines =
        [
            ("pid_10_luid_0x00000000_0x0000B002_phys_0_eng_0_engtype_3D", 30),
            ("pid_20_luid_0x00000000_0x0000B002_phys_0_eng_0_engtype_3D", 25),
            ("pid_20_luid_0x00000000_0x0000B002_phys_0_eng_3_engtype_VideoDecode", 40),
            ("pid_30_luid_0x00000000_0x0000A001_phys_0_eng_0_engtype_3D", 99),
        ];

        Assert.Equal(55.0, GpuReader.UsagePercent(engines, NvidiaLuid));
        Assert.Null(GpuReader.UsagePercent(engines, "luid_0x00000000_0x0000FFFF"));
    }

    [Fact]
    public void UsagePercent_IsCappedAt100() =>
        Assert.Equal(100.0, GpuReader.UsagePercent([("luid_0x00000000_0x0000A001_phys_0_eng_0_engtype_3D", 70), ("pid_2_luid_0x00000000_0x0000A001_phys_0_eng_0_engtype_3D", 60)], IntelLuid));

    [Fact]
    public void Sum_AddsMemoryPerGpu_AndIsNullWithoutCounters()
    {
        Assert.Equal(100UL, GpuReader.Sum([("luid_0x00000000_0x0000A001_phys_0", 100), ("luid_0x00000000_0x0000B002_phys_0", 7)], IntelLuid));
        Assert.Null(GpuReader.Sum([], IntelLuid));
    }

    [Fact]
    public void Luids_IgnoreCase() =>
        Assert.True(GpuReader.Luids([("luid_0x00000000_0x0000b002_phys_0", 1)]).SetEquals(GpuReader.Luids([("LUID_0x00000000_0x0000B002_phys_0", 2)])));

    [Fact]
    public void IsMeasured_SkipsAdaptersWithoutMemoryCounters()
    {
        IReadOnlySet<string> measured = GpuReader.Luids([("luid_0x00000000_0x0000b1e8_phys_0", 793497600), ("luid_0x00000000_0x0000cb73_phys_0", 0)]);

        Assert.True(GpuAdapter.IsMeasured("luid_0x00000000_0x0000B1E8", measured));
        // Some drivers list a duplicate entry, e.g. "RTX 3050 (2)", that has no counters.
        Assert.False(GpuAdapter.IsMeasured("luid_0x00000000_0x0000D0D0", measured));
        // Without the memory counter in Windows there is nothing to filter by.
        Assert.True(GpuAdapter.IsMeasured("luid_0x00000000_0x0000D0D0", null));
    }

    [Fact]
    public void Snapshot_PreferredIsTheBestInWindowsOrder()
    {
        var gpus = new GpuSnapshot([Intel, Nvidia]);

        Assert.Equal(Nvidia, gpus.Preferred);
        Assert.Equal(Intel, gpus.Find(Intel.Id));
        Assert.Null(new GpuSnapshot([]).Preferred);
    }

    [Fact]
    public void TotalUsedBytes_IntegratedAddsReservedAndShared()
    {
        Assert.Equal(1228 * MiB, Intel.TotalUsedBytes);
        Assert.Equal(2355 * MiB, Nvidia.TotalUsedBytes);
        Assert.Null((Intel with { DedicatedUsedBytes = null, SharedUsedBytes = null }).TotalUsedBytes);
    }

    [Fact]
    public void Sensor_TemperatureIsInTenthsOfADegree_ZeroIsNoReading()
    {
        Assert.Equal(46.9, GpuSensor.TemperatureOf(new D3dkmt.AdapterPerfData { Temperature = 469 }));
        Assert.Null(GpuSensor.TemperatureOf(new D3dkmt.AdapterPerfData()));
    }

    [Fact]
    public void AdapterPerfData_HasTheDocumentedSize() => Assert.Equal(64, Marshal.SizeOf<D3dkmt.AdapterPerfData>());
}
