using CobiHw.App;

namespace CobiHw.Tests.App;

/// <summary>Resizing the widget by dragging an edge changes its zoom.</summary>
[Collection(AppState.Name)]
public sealed class ResizeRuleTests : LocalizedTest
{
    private static readonly PixelRect Start = new(100, 100, 400, 200);
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);

    [Theory]
    [InlineData(10, 50)]
    [InlineData(500, 200)]
    [InlineData(137, 137)]
    public void ClampPercent_KeepsTheZoomBetween50And200(int percent, int expected) =>
        Assert.Equal(expected, ResizeRule.ClampPercent(percent));

    [Theory]
    [InlineData(ResizeEdge.Right, 60, 0, 120)]
    [InlineData(ResizeEdge.Left, -60, 0, 120)]
    [InlineData(ResizeEdge.Bottom, 0, 50, 150)]
    [InlineData(ResizeEdge.Top, 0, 50, 50)]
    [InlineData(ResizeEdge.BottomRight, 30, 50, 150)]
    [InlineData(ResizeEdge.Right, 30, 400, 110)]
    [InlineData(ResizeEdge.Right, 600, 0, 200)]
    [InlineData(ResizeEdge.Right, -290, 0, 50)]
    public void PercentFor_FollowsTheDraggedEdge(ResizeEdge edge, int deltaX, int deltaY, int expected) =>
        Assert.Equal(expected, ResizeRule.PercentFor(edge, 100, Start, deltaX, deltaY, WorkArea));

    [Fact]
    public void PercentFor_NeverGrowsPastTheScreen() =>
        Assert.Equal(192, ResizeRule.PercentFor(ResizeEdge.Right, 100, new PixelRect(0, 0, 1000, 500), 2000, 0, WorkArea));

    [Fact]
    public void PercentFor_StartingAt150Percent() =>
        Assert.Equal(165, ResizeRule.PercentFor(ResizeEdge.Right, 150, new PixelRect(0, 0, 450, 150), 45, 0, WorkArea));

    [Fact]
    public void Anchor_KeepsTheOppositeEdgesInPlace()
    {
        Assert.Equal(new PixelRect(40, 100, 400, 220), ResizeRule.Anchor(ResizeEdge.Left, Start, 360, 120));
        Assert.Equal(new PixelRect(40, 80, 400, 200), ResizeRule.Anchor(ResizeEdge.TopLeft, Start, 360, 120));
        Assert.Equal(new PixelRect(100, 100, 460, 220), ResizeRule.Anchor(ResizeEdge.BottomRight, Start, 360, 120));
    }

    [Theory]
    [InlineData(2.0, 50.0, ResizeEdge.Left)]
    [InlineData(298.0, 50.0, ResizeEdge.Right)]
    [InlineData(150.0, 2.0, ResizeEdge.Top)]
    [InlineData(150.0, 98.0, ResizeEdge.Bottom)]
    [InlineData(2.0, 2.0, ResizeEdge.TopLeft)]
    [InlineData(298.0, 98.0, ResizeEdge.BottomRight)]
    [InlineData(298.0, 2.0, ResizeEdge.TopRight)]
    [InlineData(2.0, 98.0, ResizeEdge.BottomLeft)]
    [InlineData(150.0, 50.0, ResizeEdge.None)]
    [InlineData(-1.0, 5.0, ResizeEdge.None)]
    public void HitTest_FindsEdgesAndCorners(double x, double y, ResizeEdge expected) =>
        Assert.Equal(expected, ResizeRule.HitTest(x, y, 300, 100, 6));

    [Fact]
    public void HitTest_OnAShortWidget_TheGrabAreaShrinks()
    {
        Assert.Equal(ResizeEdge.None, ResizeRule.HitTest(150, 5, 300, 20, 6));
        Assert.Equal(ResizeEdge.Top, ResizeRule.HitTest(150, 3, 300, 20, 6));
    }
}
