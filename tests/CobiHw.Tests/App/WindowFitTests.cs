using CobiHw.App;

namespace CobiHw.Tests.App;

/// <summary>Placement rules that keep the widget on screen.</summary>
[Collection(AppState.Name)]
public sealed class WindowFitTests : LocalizedTest
{
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void Place_TouchingTheRight_GrowsToTheLeft() =>
        Assert.Equal(new PixelRect(1520, 100, 1920, 400), WindowFit.Place(new PixelRect(1620, 100, 1920, 200), new PixelRect(1620, 100, 2020, 400), WorkArea));

    [Fact]
    public void Place_TouchingTheBottom_GrowsUpward() =>
        Assert.Equal(new PixelRect(100, 740, 400, 1040), WindowFit.Place(new PixelRect(100, 940, 400, 1040), new PixelRect(100, 940, 400, 1240), WorkArea));

    [Fact]
    public void Place_InTheBottomRightCorner_GrowsUpAndLeft() =>
        Assert.Equal(new PixelRect(1420, 640, 1920, 1040), WindowFit.Place(new PixelRect(1620, 940, 1920, 1040), new PixelRect(1620, 940, 2120, 1340), WorkArea));

    [Fact]
    public void Place_TouchingTheLeft_StaysLeft() =>
        Assert.Equal(new PixelRect(0, 100, 500, 200), WindowFit.Place(new PixelRect(0, 100, 300, 200), new PixelRect(0, 100, 500, 200), WorkArea));

    [Fact]
    public void Place_FreeButGrowingPastTheEdge_MovesBackInside() =>
        Assert.Equal(new PixelRect(1620, 500, 1920, 700), WindowFit.Place(new PixelRect(1700, 500, 1800, 600), new PixelRect(1700, 500, 2000, 700), WorkArea));

    [Fact]
    public void Place_ShrinkingWhileTouchingTheRight_StaysTouching() =>
        Assert.Equal(new PixelRect(1720, 100, 1920, 200), WindowFit.Place(new PixelRect(1420, 100, 1920, 200), new PixelRect(1420, 100, 1620, 200), WorkArea));

    [Fact]
    public void Place_LargerThanTheScreen_GoesToTheTopLeft() =>
        Assert.Equal(new PixelRect(0, 0, 3000, 2000), WindowFit.Place(new PixelRect(0, 0, 100, 100), new PixelRect(0, 0, 3000, 2000), WorkArea));

    [Fact]
    public void Place_OnAMonitorWithNegativeCoordinates() =>
        Assert.Equal(new PixelRect(-400, 0, 0, 100), WindowFit.Place(new PixelRect(-300, 0, 0, 100), new PixelRect(-300, 0, 100, 100), new PixelRect(-1920, 0, 0, 1040)));

    [Fact]
    public void Snap_CloseToAnEdge_Touches() =>
        Assert.Equal(new PixelRect(0, 300, 300, 400), WindowFit.Snap(new PixelRect(15, 300, 315, 400), WorkArea, 20));

    [Fact]
    public void Snap_FarFromTheEdges_StaysPut() =>
        Assert.Equal(new PixelRect(25, 300, 325, 400), WindowFit.Snap(new PixelRect(25, 300, 325, 400), WorkArea, 20));

    [Fact]
    public void Snap_CloseToTheRightAndBottom() =>
        Assert.Equal(new PixelRect(1620, 940, 1920, 1040), WindowFit.Snap(new PixelRect(1610, 925, 1910, 1025), WorkArea, 20));

    [Fact]
    public void Snap_AlmostAsTallAsTheScreen_GoesToTheCloserEdge() =>
        Assert.Equal(new PixelRect(100, 28, 400, 1040), WindowFit.Snap(new PixelRect(100, 15, 400, 1027), WorkArea, 20));

    [Fact]
    public void Snap_WithEqualDistances_PrefersTheStart() =>
        Assert.Equal(new PixelRect(100, 0, 400, 1020), WindowFit.Snap(new PixelRect(100, 10, 400, 1030), WorkArea, 20));

    [Fact]
    public void Snap_TheCloserEdgeWins() =>
        Assert.Equal(new PixelRect(100, 20, 400, 1040), WindowFit.Snap(new PixelRect(100, 15, 400, 1035), WorkArea, 20));

    [Fact]
    public void Snap_OnAMonitorWithNegativeCoordinates() =>
        Assert.Equal(new PixelRect(-1920, 0, -1620, 100), WindowFit.Snap(new PixelRect(-1915, 10, -1615, 110), new PixelRect(-1920, 0, 0, 1040), 20));

    [Fact]
    public void MoveTo_KeepsTheRelativePosition()
    {
        var first = new PixelRect(0, 0, 1920, 1040);
        var second = new PixelRect(1920, 0, 4480, 1400);

        Assert.Equal(new PixelRect(4180, 0, 4480, 100), WindowFit.MoveTo(new PixelRect(1620, 0, 1920, 100), first, second));
        Assert.Equal(new PixelRect(1920, 1300, 2220, 1400), WindowFit.MoveTo(new PixelRect(0, 940, 300, 1040), first, second));
        Assert.Equal(new PixelRect(3050, 650, 3350, 750), WindowFit.MoveTo(new PixelRect(810, 470, 1110, 570), first, second));
    }

    [Fact]
    public void MoveTo_LargerThanTheTarget_GoesToTheCorner() =>
        Assert.Equal(new PixelRect(0, 0, 3000, 100), WindowFit.MoveTo(new PixelRect(0, 0, 3000, 100), new PixelRect(1920, 0, 4480, 1400), WorkArea));

    [Fact]
    public void BringInside_FromAMonitorThatWasRemoved()
    {
        Assert.Equal(new PixelRect(1620, 900, 1920, 960), WindowFit.BringInside(new PixelRect(2600, 900, 2900, 960), WorkArea));
        Assert.Equal(new PixelRect(0, 980, 300, 1040), WindowFit.BringInside(new PixelRect(-1500, 1300, -1200, 1360), WorkArea));
    }

    [Fact]
    public void BringInside_AlreadyInside_DoesNotMove()
    {
        var inside = new PixelRect(10, 10, 310, 70);

        Assert.True(WindowFit.IsInside(inside, WorkArea));
        Assert.Equal(inside, WindowFit.BringInside(inside, WorkArea));
    }

    [Fact]
    public void BringInside_HalfOutsideAfterAResolutionChange()
    {
        var halfOut = new PixelRect(1800, 500, 2100, 560);

        Assert.False(WindowFit.IsInside(halfOut, WorkArea));
        Assert.Equal(new PixelRect(1620, 500, 1920, 560), WindowFit.BringInside(halfOut, WorkArea));
    }
}
