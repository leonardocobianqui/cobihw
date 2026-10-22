using CobiHw.App;
using CobiHw.App.Settings;
using CobiHw.App.Tray;

namespace CobiHw.Tests.App;

/// <summary>Monitor numbering, click-through keys, theme choice and the tray tooltip limit.</summary>
[Collection(AppState.Name)]
public sealed class SmallRulesTests : LocalizedTest
{
    [Fact]
    public void Monitors_AreNumberedFromLeftToRight_IgnoringTheWindowsName()
    {
        List<DisplayMonitor> monitors = DisplayMonitor.ByPosition(
        [
            new(@"\\.\DISPLAY33", 0, 0, 1920, 1080, true),
            new(@"\\.\DISPLAY5", -2560, 0, 2560, 1440, false),
            new(@"\\.\DISPLAY7", 1920, -300, 1080, 1920, false),
        ]);

        Assert.Equal(new[] { (@"\\.\DISPLAY5", 1), (@"\\.\DISPLAY33", 2), (@"\\.\DISPLAY7", 3) }, monitors.Select(monitor => (monitor.Device, monitor.Number)));
        Assert.True(monitors[1].IsPrimary);
        Assert.Equal(1080, monitors[2].Width);
    }

    [Fact]
    public void Monitors_StackedVertically_TopComesFirst()
    {
        List<DisplayMonitor> monitors = DisplayMonitor.ByPosition([new("B", 0, 1080, 1920, 1080, false), new("A", 0, 0, 1920, 1080, true)]);

        Assert.Equal("A", monitors[0].Device);
        Assert.Equal(2, monitors[1].Number);
    }

    [Fact]
    public void ClickThrough_IsHolding_NeedsEveryKeyOfTheCombination()
    {
        var down = new HashSet<int>();
        bool IsDown(int key) => down.Contains(key);

        Assert.False(ClickThroughRule.IsHolding(InteractionKey.Ctrl, IsDown));

        down.Add(ClickThroughRule.ControlKey);
        Assert.True(ClickThroughRule.IsHolding(InteractionKey.Ctrl, IsDown));
        Assert.False(ClickThroughRule.IsHolding(InteractionKey.CtrlShift, IsDown));
        Assert.False(ClickThroughRule.IsHolding(InteractionKey.Shift, IsDown));

        down.Add(ClickThroughRule.ShiftKey);
        Assert.True(ClickThroughRule.IsHolding(InteractionKey.CtrlShift, IsDown));
        Assert.True(ClickThroughRule.IsHolding(InteractionKey.Ctrl, IsDown));
        Assert.False(ClickThroughRule.IsHolding(InteractionKey.CtrlAlt, IsDown));
    }

    [Fact]
    public void ClickThrough_None_IsNeverHolding() => Assert.False(ClickThroughRule.IsHolding(InteractionKey.None, _ => true));

    [Fact]
    public void Theme_FixedChoicesIgnoreWindows_SystemFollowsIt()
    {
        Assert.True(WindowsTheme.IsDark(AppTheme.Dark, windowsUsesDark: false));
        Assert.False(WindowsTheme.IsDark(AppTheme.Light, windowsUsesDark: true));
        Assert.True(WindowsTheme.IsDark(AppTheme.System, windowsUsesDark: true));
        Assert.False(WindowsTheme.IsDark(AppTheme.System, windowsUsesDark: false));
    }

    [Fact]
    public void TrayTooltip_WhenItFits_KeepsEveryLine() => Assert.Equal("cobihw\na\nb", TrayTooltip.Build(["cobihw", "a", "b"]));

    [Fact]
    public void TrayTooltip_PastTheLimit_DropsWholeLinesFromTheEnd()
    {
        var lines = new List<string> { "cobihw" };
        for (int i = 0; i < 10; i++)
        {
            lines.Add(new string('x', 20) + i);
        }

        string text = TrayTooltip.Build(lines);
        string[] kept = text.Split('\n');

        Assert.True(text.Length <= TrayTooltip.MaximumLength, $"{text.Length} characters");
        Assert.Equal("cobihw", kept[0]);
        Assert.Equal(6, kept.Length);
        Assert.All(kept.Skip(1), line => Assert.Equal(21, line.Length));
    }
}
