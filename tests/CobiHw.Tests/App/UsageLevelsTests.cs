using System.Globalization;
using CobiHw.App;
using CobiHw.App.Settings;

namespace CobiHw.Tests.App;

/// <summary>Usage levels pick the normal, warning and critical colors.</summary>
[Collection(AppState.Name)]
public sealed class UsageLevelsTests : LocalizedTest
{
    [Theory]
    [InlineData(49.4, UsageLevel.Normal)]
    [InlineData(49.5, UsageLevel.Warning)]
    [InlineData(74.4, UsageLevel.Warning)]
    [InlineData(74.6, UsageLevel.Critical)]
    [InlineData(100.0, UsageLevel.Critical)]
    [InlineData(0.0, UsageLevel.Normal)]
    public void Classify_ComparesTheValueAsShownOnScreen(double percent, UsageLevel expected) =>
        Assert.Equal(expected, UsageLevels.Classify(percent, UsageThresholds.Default, enabled: true));

    [Fact]
    public void Classify_WithoutReadingOrWithColorsOff_IsNone()
    {
        Assert.Equal(UsageLevel.None, UsageLevels.Classify(null, UsageThresholds.Default, enabled: true));
        Assert.Equal(UsageLevel.None, UsageLevels.Classify(90, UsageThresholds.Default, enabled: false));
    }

    [Fact]
    public void PercentFormat_RoundsTheSameWayAsClassify()
    {
        // "{0:0}" rounds half away from zero, matching the rounding inside Classify.
        Assert.Equal("50", string.Format(CultureInfo.InvariantCulture, "{0:0}", 49.5));
        Assert.Equal("75", string.Format(CultureInfo.InvariantCulture, "{0:0}", 74.5));
    }

    [Theory]
    [InlineData(21, UsageLevel.Normal)]
    [InlineData(20, UsageLevel.Warning)]
    [InlineData(11, UsageLevel.Warning)]
    [InlineData(10, UsageLevel.Critical)]
    public void ClassifyBattery_UsesFixedLimits(int percent, UsageLevel expected) =>
        Assert.Equal(expected, UsageLevels.ClassifyBattery(percent, enabled: true));

    [Fact]
    public void ClassifyBattery_WithoutReadingOrWithColorsOff_IsNone()
    {
        Assert.Equal(UsageLevel.None, UsageLevels.ClassifyBattery(null, enabled: true));
        Assert.Equal(UsageLevel.None, UsageLevels.ClassifyBattery(5, enabled: false));
    }
}
