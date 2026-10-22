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
}
