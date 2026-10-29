using CobiHw.App;

namespace CobiHw.Tests.App;

/// <summary>Reading and writing "#RRGGBB" colors.</summary>
[Collection(AppState.Name)]
public sealed class HexColorTests : LocalizedTest
{
    [Theory]
    [InlineData(" #1e1f22 ", "#1E1F22")]
    [InlineData("vermelho", "#000000")]
    [InlineData("#12345", "#000000")]
    public void Normalize_AcceptsOnlyRrggbb(string text, string expected) =>
        Assert.Equal(expected, HexColor.Normalize(text, "#000000"));

    [Fact]
    public void Normalize_NullUsesTheFallback() => Assert.Equal("#ABCDEF", HexColor.Normalize(null, "#ABCDEF"));

    [Fact]
    public void WithAlpha_AndFromRgb_WriteUppercaseHex()
    {
        Assert.Equal("#801E1F22", HexColor.WithAlpha("#1E1F22", 0x80));
        Assert.Equal("#FF0010", HexColor.FromRgb(255, 0, 16));
    }

    [Theory]
    [InlineData("#FFFFFF", "#000000", 50, "#808080")]
    [InlineData("#E6E6E6", "#1E1F22", 100, "#E6E6E6")]
    [InlineData("#E6E6E6", "#1E1F22", 0, "#1E1F22")]
    public void Mix_IsTheColorDrawnOverTheBackground(string color, string background, int opacityPercent, string expected) =>
        Assert.Equal(expected, HexColor.Mix(color, background, opacityPercent / 100.0));

    [Theory]
    [InlineData("#E6E6E6", "#000000")]
    [InlineData("#FFD400", "#000000")]
    [InlineData("#1B1B1B", "#FFFFFF")]
    [InlineData("#0000FF", "#FFFFFF")]
    public void Contrasting_PicksBlackOrWhite(string color, string expected) =>
        Assert.Equal(expected, HexColor.Contrasting(color));
}
