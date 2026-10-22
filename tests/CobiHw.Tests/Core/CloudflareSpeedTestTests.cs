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
    public void ServerMilliseconds_ReadsServerTiming()
    {
        var timed = new HttpResponseMessage();
        timed.Headers.TryAddWithoutValidation("Server-Timing", "cfRequestDuration;dur=1.5");

        Assert.Equal(1.5, CloudflareSpeedTest.ServerMilliseconds(timed));
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

        SpeedTestResult result = await new CloudflareSpeedTest(new HttpClient(server)).RunAsync(progress, TestContext.Current.CancellationToken);
        IReadOnlyList<SpeedTestProgress> reports = progress.Reports;

        Assert.InRange(result.DownloadBitsPerSecond, 0.7e9, 1.3e9);
        Assert.InRange(result.UploadBitsPerSecond, 0.7e9, 1.3e9);
        // 5 ms simulated latency minus the 1.5 ms the server reports as its own time.
        Assert.InRange(result.LatencyMilliseconds, 2, 15);
        Assert.True(server.UploadLengthsMatched);
        // Never asks for 100 MB or more (the server refuses it).
        Assert.Equal(50_000_000, server.LargestDownload);
        Assert.Equal(5, server.LargestDownloadCount);

        // Steps alternate like the Cloudflare page does.
        SpeedTestStage[] stageChanges = [.. reports.Where(report => report.BitsPerSecond is null && report.LatencyMilliseconds is null).Select(report => report.Stage)];
        Assert.Equal(
            new[] { SpeedTestStage.Latency, SpeedTestStage.Download, SpeedTestStage.Upload, SpeedTestStage.Download, SpeedTestStage.Upload, SpeedTestStage.Download, SpeedTestStage.Upload, SpeedTestStage.Download, SpeedTestStage.Upload, SpeedTestStage.Download },
            stageChanges);

        SpeedTestProgress[] latencies = [.. reports.Where(report => report.LatencyMilliseconds is not null)];
        Assert.Equal(20, latencies.Length);
        Assert.All(latencies, report => Assert.Equal(SpeedTestStage.Latency, report.Stage));

        double[] liveDownload = [.. reports.Where(report => report.Stage == SpeedTestStage.Download && report.BitsPerSecond is not null).Select(report => report.BitsPerSecond!.Value)];
        double[] liveUpload = [.. reports.Where(report => report.Stage == SpeedTestStage.Upload && report.BitsPerSecond is not null).Select(report => report.BitsPerSecond!.Value)];
        Assert.True(liveDownload.Length > 20, $"{liveDownload.Length} live download readings");
        Assert.True(liveUpload.Length > 15, $"{liveUpload.Length} live upload readings");
        Assert.All(liveDownload.TakeLast(4), speed => Assert.InRange(speed, 0.7e9, 1.3e9));
        Assert.All(liveUpload.TakeLast(4), speed => Assert.InRange(speed, 0.7e9, 2e9));
    }

    [Fact]
    public async Task RunAsync_At100Mbps_StopsGrowingAfterOneSecond()
    {
        var server = new FakeCloudflare(100e6);

        SpeedTestResult result = await new CloudflareSpeedTest(new HttpClient(server)).RunAsync(null, TestContext.Current.CancellationToken);

        // The 25 MB download takes over 1 s at 100 Mbps, so the sizes stop growing there.
        Assert.Equal(25_000_000, server.LargestDownload);
        Assert.InRange(result.DownloadBitsPerSecond, 70e6, 130e6);
    }
}
