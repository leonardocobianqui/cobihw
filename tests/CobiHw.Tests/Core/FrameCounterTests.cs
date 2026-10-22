using CobiHw.Core.Fps;

namespace CobiHw.Tests.Core;

/// <summary>Frames per second from the presentation events of each process.</summary>
[Collection(AppState.Name)]
public sealed class FrameCounterTests
{
    private static TimeSpan Ms(double value) => TimeSpan.FromMilliseconds(value);

    private static FrameCounter Steady(int process, double framesPerSecond, int frames, double startMs = 0)
    {
        var counter = new FrameCounter();
        Record(counter, process, framesPerSecond, frames, startMs);
        return counter;
    }

    private static void Record(FrameCounter counter, int process, double framesPerSecond, int frames, double startMs = 0)
    {
        for (int i = 0; i <= frames; i++)
        {
            counter.Record(process, Ms(startMs + i * 1000.0 / framesPerSecond));
        }
    }

    [Fact]
    public void Rate_SteadySixty_IsSixty() =>
        Assert.Equal(60, Steady(10, 60, 120).Rate(10, Ms(2000))!.Value, 0.5);

    [Fact]
    public void Rate_ProcessWithoutFrames_IsNull() =>
        Assert.Null(Steady(10, 60, 120).Rate(99, Ms(2000)));

    [Fact]
    public void Rate_LastFrameThreeSecondsOld_StillMeasures()
    {
        // ETW delivers events a few seconds late, so a 3 s old frame is still "now".
        Assert.Equal(60, Steady(10, 60, 120).Rate(10, Ms(5000))!.Value, 0.5);
    }

    [Fact]
    public void Rate_NoFramesForOverFourSeconds_ExpiresForGood()
    {
        FrameCounter counter = Steady(10, 60, 120);

        Assert.Null(counter.Rate(10, Ms(6100)));
        Assert.Null(counter.Rate(10, Ms(2000)));
    }

    [Fact]
    public void Rate_SingleFrame_IsNull()
    {
        var counter = new FrameCounter();
        counter.Record(5, Ms(100));

        Assert.Null(counter.Rate(5, Ms(150)));
    }

    [Fact]
    public void Rate_UsesTheLastSecond()
    {
        var counter = new FrameCounter();
        Record(counter, 7, 30, 59);
        Record(counter, 7, 120, 180, startMs: 2000);

        Assert.Equal(120, counter.Rate(7, Ms(3500))!.Value, 1.0);
    }

    [Fact]
    public void Rate_OutOfOrderEvents_StillSixty()
    {
        var counter = new FrameCounter();
        List<double> times = [.. Enumerable.Range(0, 61).Select(i => i * 1000.0 / 60)];
        for (int i = 0; i + 1 < times.Count; i += 2)
        {
            (times[i], times[i + 1]) = (times[i + 1], times[i]);
        }

        foreach (double time in times)
        {
            counter.Record(3, Ms(time));
        }

        Assert.Equal(60, counter.Rate(3, Ms(1000))!.Value, 0.5);
    }

    [Fact]
    public void Rate_IsCountedPerProcess()
    {
        var counter = new FrameCounter();
        for (int i = 0; i <= 60; i++)
        {
            counter.Record(1, Ms(i * 1000.0 / 60));
            counter.Record(2, Ms(i * 1000.0 / 30));
        }

        Assert.Equal(60, counter.Rate(1, Ms(1500))!.Value, 0.5);
        Assert.Equal(30, counter.Rate(2, Ms(2000))!.Value, 0.5);
    }
}
