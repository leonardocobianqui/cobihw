using System.Diagnostics;
using CobiHw.Core.Linux;
using CobiHw.Core.Linux.Readers;
using CobiHw.Core.Tests.Support;

namespace CobiHw.Core.Tests.Linux;

/// <summary>Network speeds and session totals from /sys/class/net.</summary>
public sealed class NetworkReaderTests
{
    private const long Start = 1_000;

    private static long Seconds(int seconds) => Start + seconds * Stopwatch.Frequency;

    [Fact]
    public void Notebook_OnlyTheWifiCountsInTheTotal_TailscaleAndLoopbackStayOut()
    {
        using FakeSystem system = Notebook.Build();
        var reader = new NetworkReader(system.Files, null);
        reader.Read(Start);

        Notebook.Interface(system, "lo", 1, "unknown", physical: false, 2448149 + 5_000, 2448149 + 5_000);
        Notebook.Interface(system, "wlp2s0", 2, "up", physical: true, 2325411943 + 1_000_000, 426548643 + 50_000);
        Notebook.Interface(system, "tailscale0", 3, "unknown", physical: false, 1990 + 250_000, 102051);
        NetworkSnapshot second = reader.Read(Seconds(1));

        Assert.Equal(1_000_000, second.DownloadBytesPerSecond!.Value, 1.0);
        Assert.Equal(50_000, second.UploadBytesPerSecond!.Value, 1.0);
        Assert.Equal(250_000, second.Find(InterfaceId.From("tailscale0"))!.DownloadBytesPerSecond!.Value, 1.0);
        Assert.Equal("wlp2s0", second.Find(InterfaceId.From("wlp2s0"))!.Name);
        Assert.True(second.Find(InterfaceId.From("wlp2s0"))!.IsUp);
    }

    [Fact]
    public void Up_FromOperstate_OrFromTheCarrierWhenTheDriverDoesNotTrackIt()
    {
        Assert.True(NetworkReader.IsUp("up", null));
        Assert.False(NetworkReader.IsUp("down", 1));
        Assert.True(NetworkReader.IsUp("unknown", 1));
        Assert.False(NetworkReader.IsUp("unknown", 0));
        Assert.False(NetworkReader.IsUp("dormant", null));
    }

    [Fact]
    public void SavedSession_ResumesOnTheSameBoot_AndIsDiscardedAfterAReboot()
    {
        DateTime boot = NetworkReader.BootTimeUtc(Notebook.Stat)!.Value;
        NetworkSessionState saved = NetworkSessionState.CreateForBoot(boot);
        saved.ReceivedBytes = 123;

        using FakeSystem sameBoot = Notebook.Build();
        using FakeSystem rebooted = Notebook.Build().File("proc/stat", Notebook.Stat.Replace("btime 1793993870", "btime 1794080270"));

        Assert.Equal(new DateTime(2026, 11, 6, 19, 37, 50, DateTimeKind.Utc), boot);
        Assert.Equal(123UL, new NetworkReader(sameBoot.Files, saved).ExportSession().ReceivedBytes);
        Assert.Equal(0UL, new NetworkReader(rebooted.Files, saved).ExportSession().ReceivedBytes);
    }

    [Fact]
    public void AnInterfaceThatDisappears_IsSkipped()
    {
        using FakeSystem system = Notebook.Build();
        system.Remove("sys/class/net/tailscale0/statistics");

        NetworkSnapshot snapshot = new NetworkReader(system.Files, null).Read(Start);

        Assert.Null(snapshot.Find(InterfaceId.From("tailscale0")));
        Assert.Equal(2, snapshot.Devices.Count);
    }
}
