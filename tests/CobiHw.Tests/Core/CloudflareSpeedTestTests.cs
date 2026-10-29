using System.Net.Http;
using CobiHw.Core.Network;
using CobiHw.Tests.Fakes;

namespace CobiHw.Tests.Core;

/// <summary>Speed test math and a full run against the simulated Cloudflare server.</summary>
[Collection(AppState.Name)]
public sealed class CloudflareSpeedTestTests
{
    [Fact]
    public void Percentile_Interpolates()
    {
        Assert.Equal(91.0, CloudflareSpeedTest.Percentile([10, 20, 30, 40, 50, 60, 70, 80, 90, 100], 0.9), 9);
        Assert.Equal(2.0, CloudflareSpeedTest.Percentile([3, 1, 2], 0.5));
    }

    [Fact]
    public void Jitter_IsTheAverageDifferenceBetweenSamples() =>
        Assert.Equal(2.0, CloudflareSpeedTest.Jitter([10, 14, 12, 12]), 9);

    [Fact]
    public void ServerMilliseconds_ReadsServerTiming_LikeCloudflare()
    {
        Assert.Equal(1.5, CloudflareSpeedTest.ServerMilliseconds(Timed("cfRequestDuration;dur=1.5")));
        // What speed.cloudflare.com sends today: the speed parts add up, and the TCP details (cfL4) don't count.
        Assert.Equal(34.0, CloudflareSpeedTest.ServerMilliseconds(Timed(
            "cfSpeedEdge;dur=6, cfSpeedWorker;dur=28",
            "cfL4;desc=\"?proto=TCP&rtt=20128&min_rtt=14818\"")));
        // The request duration wins over the parts.
        Assert.Equal(12.0, CloudflareSpeedTest.ServerMilliseconds(Timed("cfSpeedEdge;dur=6, cfRequestDuration;dur=12")));
        // Without a usable value, the estimate.
        Assert.Equal(10.0, CloudflareSpeedTest.ServerMilliseconds(Timed("cfL4;desc=\"?rtt=20\"")));
        Assert.Equal(10.0, CloudflareSpeedTest.ServerMilliseconds(new HttpResponseMessage()));
    }

    [Fact]
    public void StageOf_TellsTheStepOfARequest()
    {
        Assert.Equal(SpeedTestStage.Latency, CloudflareSpeedTest.StageOf(true, 0));
        Assert.Equal(SpeedTestStage.Download, CloudflareSpeedTest.StageOf(true, 100_000_000));
        Assert.Equal(SpeedTestStage.Upload, CloudflareSpeedTest.StageOf(false, 100_000));
    }

    [Fact]
    public async Task RunAsync_At1Gbps_MeasuresSpeedLatencyAndReportsProgress()
    {
        var server = new FakeCloudflare();
        var progress = new InlineProgress();

        SpeedTestResult result = await FakeCloudflare.Client(server).RunAsync(progress, TestContext.Current.CancellationToken);
        IReadOnlyList<SpeedTestProgress> reports = progress.Reports;

        Assert.InRange(result.DownloadBitsPerSecond, 0.8e9, 1.2e9);
        Assert.InRange(result.UploadBitsPerSecond, 0.8e9, 1.2e9);
        // 5 ms simulated latency minus the 1.5 ms the server reports as its own time.
        Assert.InRange(result.LatencyMilliseconds, 2, 15);
        // Never asks for 100 MB or more (the server refuses it), and runs 4 connections at once.
        Assert.Equal(50_000_000, server.LargestDownload);
        Assert.Equal(4, server.MostTransfers);
        // When the upload phase ends, the uploads stop sending and finish as complete requests.
        Assert.Equal(0, server.CutUploads);

        // One phase at a time, like Ookla's Speedtest.
        SpeedTestStage[] stageChanges = [.. reports.Where(report => report.BitsPerSecond is null && report.LatencyMilliseconds is null).Select(report => report.Stage)];
        Assert.Equal(new[] { SpeedTestStage.Latency, SpeedTestStage.Download, SpeedTestStage.Upload }, stageChanges);

        SpeedTestProgress[] latencies = [.. reports.Where(report => report.LatencyMilliseconds is not null)];
        Assert.Equal(20, latencies.Length);
        Assert.All(latencies, report => Assert.Equal(SpeedTestStage.Latency, report.Stage));
        // The live latency ends on the result.
        Assert.Equal(result.LatencyMilliseconds, latencies[^1].LatencyMilliseconds);
        Assert.Equal(result.JitterMilliseconds, latencies[^1].JitterMilliseconds);

        // A live speed every 250 ms (1 s phases), and each phase's last update is its result.
        double[] liveDownload = [.. reports.Where(report => report.Stage == SpeedTestStage.Download && report.BitsPerSecond is not null).Select(report => report.BitsPerSecond!.Value)];
        double[] liveUpload = [.. reports.Where(report => report.Stage == SpeedTestStage.Upload && report.BitsPerSecond is not null).Select(report => report.BitsPerSecond!.Value)];
        Assert.True(liveDownload.Length >= 4, $"{liveDownload.Length} live download readings");
        Assert.True(liveUpload.Length >= 4, $"{liveUpload.Length} live upload readings");
        Assert.Equal(result.DownloadBitsPerSecond, liveDownload[^1]);
        Assert.Equal(result.UploadBitsPerSecond, liveUpload[^1]);
    }

    [Fact]
    public async Task RunAsync_At100Mbps_TheConnectionsShareTheLine()
    {
        var server = new FakeCloudflare(100e6);

        SpeedTestResult result = await FakeCloudflare.Client(server).RunAsync(null, TestContext.Current.CancellationToken);

        Assert.InRange(result.DownloadBitsPerSecond, 80e6, 120e6);
        Assert.InRange(result.UploadBitsPerSecond, 80e6, 120e6);
        Assert.Equal(4, server.MostTransfers);
        Assert.Equal(0, server.CutUploads);
    }

    [Fact]
    public async Task RunAsync_EachPhaseRunsForItsTime()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        await FakeCloudflare.Client(new FakeCloudflare(100e6)).RunAsync(null, TestContext.Current.CancellationToken);

        // Two 1 s phases, plus the latency phase and the connections stopping.
        Assert.InRange(clock.Elapsed, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task RunAsync_LeavesTheRampUpOut()
    {
        // The upload crawls for its first 450 ms; the phase lasts 1 s and its first 500 ms don't count.
        var server = new FakeCloudflare(1e9, uploadSlowStart: TimeSpan.FromMilliseconds(450));
        var timing = new SpeedTestTiming(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500), 4);

        SpeedTestResult result = await new CloudflareSpeedTest(new HttpClient(server), timing).RunAsync(null, TestContext.Current.CancellationToken);

        // Averaged over the whole phase it would be about 0.55 Gbps.
        Assert.InRange(result.UploadBitsPerSecond, 0.8e9, 1.2e9);
    }

    private static HttpResponseMessage Timed(params string[] serverTimings)
    {
        var response = new HttpResponseMessage();
        foreach (string serverTiming in serverTimings)
        {
            response.Headers.TryAddWithoutValidation("Server-Timing", serverTiming);
        }

        return response;
    }
}
