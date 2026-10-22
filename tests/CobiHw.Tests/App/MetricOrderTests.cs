using CobiHw.App.Settings;

namespace CobiHw.Tests.App;

/// <summary>Order of the items and which of them are shown.</summary>
[Collection(AppState.Name)]
public sealed class MetricOrderTests : LocalizedTest
{
    [Fact]
    public void Default_IsCpuGpuRamDiskNetworkBattery() =>
        Assert.Equal(new[] { MetricItem.Cpu, MetricItem.Gpu, MetricItem.Memory, MetricItem.Disk, MetricItem.Network, MetricItem.Battery }, MetricOrder.Default.Items);

    [Fact]
    public void IncompleteRepeatedOrInvalidOrder_IsCompleted() =>
        Assert.Equal(
            new[] { MetricItem.Network, MetricItem.Cpu, MetricItem.Gpu, MetricItem.Memory, MetricItem.Disk, MetricItem.Battery },
            new MetricOrder([MetricItem.Network, MetricItem.Network, (MetricItem)99, MetricItem.Cpu]).Items);

    [Fact]
    public void EqualOrders_AreEqual_AlsoInsideDisplaySettings()
    {
        Assert.Equal(new MetricOrder([MetricItem.Disk]), new MetricOrder([MetricItem.Disk]));
        Assert.Equal(new DisplaySettings { Order = new([MetricItem.Disk]) }, new DisplaySettings { Order = new([MetricItem.Disk]) });
        Assert.NotEqual(new DisplaySettings(), new DisplaySettings { Order = new([MetricItem.Disk]) });
    }

    [Fact]
    public void Fps_IsNotPartOfTheDraggableOrder()
    {
        Assert.DoesNotContain(MetricItem.Fps, MetricOrder.Orderable);
        Assert.DoesNotContain(MetricItem.Fps, MetricOrder.Default.Items);
        Assert.Equal(6, MetricOrder.Default.Items.Count);
    }

    [Fact]
    public void Fps_SavedInTheOrder_IsIgnored() =>
        Assert.Equal(
            new[] { MetricItem.Disk, MetricItem.Cpu, MetricItem.Gpu, MetricItem.Memory, MetricItem.Network, MetricItem.Battery },
            new MetricOrder([MetricItem.Fps, MetricItem.Disk]).Items);

    [Fact]
    public void Fps_IsOffByDefault_AndIsShownFollowsTheOption()
    {
        Assert.False(new DisplaySettings().ShowFps);
        Assert.False(new DisplaySettings().IsShown(MetricItem.Fps));
        Assert.True(new DisplaySettings { ShowFps = true }.IsShown(MetricItem.Fps));
    }
}
