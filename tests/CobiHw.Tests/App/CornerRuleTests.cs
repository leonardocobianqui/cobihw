using CobiHw.App;

namespace CobiHw.Tests.App;

/// <summary>Rounded corners must never cut into the content.</summary>
[Collection(AppState.Name)]
public sealed class CornerRuleTests : LocalizedTest
{
    private static readonly Inset[] Layouts = [CornerRule.Full, CornerRule.CompactHorizontal, CornerRule.CompactVertical, CornerRule.Details];

    [Fact]
    public void LargestRadius_OfEachLayout()
    {
        Assert.Equal(36.97, CornerRule.LargestRadius(CornerRule.CompactHorizontal), 2);
        Assert.Equal(30.65, CornerRule.LargestRadius(CornerRule.Details), 2);
    }

    [Fact]
    public void MaximumRadius_IsTheSmallestLimitRoundedDown() => Assert.Equal(30, CornerRule.MaximumRadius);

    [Fact]
    public void AtTheMaximum_TheContentCornerStaysInsideTheCurve()
    {
        foreach (Inset layout in Layouts)
        {
            Assert.True(CornerInside(layout, CornerRule.MaximumRadius), $"{layout} at {CornerRule.MaximumRadius}");
        }
    }

    [Fact]
    public void AtTheExactLimit_StaysInside_HalfAPixelMore_CutsIn()
    {
        foreach (Inset layout in Layouts)
        {
            double limit = CornerRule.LargestRadius(layout);
            Assert.True(CornerInside(layout, limit), $"{layout} at {limit}");
            Assert.False(CornerInside(layout, limit + 0.5), $"{layout} at {limit + 0.5}");
        }
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(99, 30)]
    [InlineData(12, 12)]
    public void Clamp_KeepsTheRadiusInRange(int radius, int expected) => Assert.Equal(expected, CornerRule.Clamp(radius));

    // True when the content corner (h, v) is inside the corner circle of the given radius.
    private static bool CornerInside(Inset inset, double radius) =>
        inset.Horizontal >= radius || inset.Vertical >= radius ||
        Math.Pow(radius - inset.Horizontal, 2) + Math.Pow(radius - inset.Vertical, 2) <= radius * radius + 1e-9;
}
