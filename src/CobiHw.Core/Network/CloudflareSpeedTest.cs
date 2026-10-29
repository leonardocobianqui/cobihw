using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;

namespace CobiHw.Core.Network;

/// <summary>
/// Internet speed test against speed.cloudflare.com: latency as Cloudflare's own page measures it,
/// bandwidth the way Ookla's Speedtest does.
/// </summary>
/// <remarks>
/// Latency is the median time to first byte of empty downloads, minus the server time from the Server-Timing header.
/// Each bandwidth phase runs several connections at once for a fixed time; the first seconds, while TCP speeds up,
/// don't count, and the result is the average speed of the rest.
/// </remarks>
public sealed class CloudflareSpeedTest
{
    public const string DownloadUrl = "https://speed.cloudflare.com/__down";
    public const string UploadUrl = "https://speed.cloudflare.com/__up";

    // Latency = median of 20 empty downloads, like speed.cloudflare.com.
    private const int LatencySamples = 20;
    private const double LatencyPercentile = 0.5;
    // Used when the response has no Server-Timing header.
    private const double DefaultServerMilliseconds = 10;
    // Size of each bandwidth request, under the 100 MB the server accepts. Bytes count as they move:
    // at the end of a phase downloads are cut, and uploads stop sending and finish as complete requests.
    private const long RequestBytes = 50_000_000;
    // After its phase ends, an upload gets this long for the server's answer before it's dropped.
    private static readonly TimeSpan UploadAnswerTimeout = TimeSpan.FromSeconds(1);
    // The live speed is the traffic of the last second, updated every 250 ms.
    private const int LiveWindowSamples = 4;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient _http;
    private readonly SpeedTestTiming _timing;

    /// <summary>Uses the given client, so tests can replace the network with a fake handler.</summary>
    public CloudflareSpeedTest(HttpClient http)
        : this(http, SpeedTestTiming.Default)
    {
    }

    /// <summary>Uses the given client and phase timing; tests use shorter phases.</summary>
    internal CloudflareSpeedTest(HttpClient http, SpeedTestTiming timing)
    {
        _http = http;
        _timing = timing;
    }

    private enum Direction
    {
        Download,
        Upload,
    }

    /// <summary>Runs latency, download and upload measurements, one after the other.</summary>
    /// <param name="progress">Receives live updates; may be null.</param>
    /// <param name="cancellationToken">Stops the test.</param>
    /// <exception cref="SpeedTestException">The test couldn't complete.</exception>
    public async Task<SpeedTestResult> RunAsync(IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new SpeedTestProgress(SpeedTestStage.Latency, null, null));
        // Warm-up requests open the connection, so the first latency sample doesn't include the TLS handshake.
        await MeasureAsync(Direction.Download, 0, null, cancellationToken, CancellationToken.None).ConfigureAwait(false);
        await MeasureAsync(Direction.Download, 100_000, null, cancellationToken, CancellationToken.None).ConfigureAwait(false);

        var latencies = new List<double>(LatencySamples);
        for (int i = 0; i < LatencySamples; i++)
        {
            latencies.Add(await MeasureAsync(Direction.Download, 0, null, cancellationToken, CancellationToken.None).ConfigureAwait(false));
            progress?.Report(new SpeedTestProgress(SpeedTestStage.Latency, null, Percentile(latencies, LatencyPercentile), Jitter(latencies)));
        }

        double download = await MeasureBandwidthAsync(Direction.Download, progress, cancellationToken).ConfigureAwait(false);
        double upload = await MeasureBandwidthAsync(Direction.Upload, progress, cancellationToken).ConfigureAwait(false);

        return new SpeedTestResult(
            DateTimeOffset.Now,
            download,
            upload,
            Percentile(latencies, LatencyPercentile),
            Jitter(latencies));
    }

    /// <summary>Linear-interpolated percentile (0.5 = median).</summary>
    internal static double Percentile(IReadOnlyCollection<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            throw new SpeedTestException(SpeedTestFailure.NoValidMeasurement);
        }

        double[] sorted = [.. values.Order()];
        double position = (sorted.Length - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);

        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    /// <summary>Mean absolute difference between consecutive latency samples.</summary>
    internal static double Jitter(IReadOnlyList<double> latencies)
    {
        if (latencies.Count < 2)
        {
            return 0;
        }

        double total = 0;
        for (int i = 1; i < latencies.Count; i++)
        {
            total += Math.Abs(latencies[i] - latencies[i - 1]);
        }

        return total / (latencies.Count - 1);
    }

    /// <summary>Server processing time from the Server-Timing header, read the way Cloudflare's own test reads it.</summary>
    /// <remarks>
    /// "cfRequestDuration;dur=X" wins when present; otherwise the "cfSpeed…;dur=X" parts add up
    /// (e.g. "cfSpeedEdge;dur=6, cfSpeedWorker;dur=28" is 34 ms). Other parts, such as "cfL4", don't count.
    /// </remarks>
    internal static double ServerMilliseconds(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Server-Timing", out IEnumerable<string>? values))
        {
            return DefaultServerMilliseconds;
        }

        double? requestDuration = null;
        double speedParts = 0;

        // Each metric is "name;param=value;…", and metrics are separated by commas.
        foreach (string metric in values.SelectMany(value => value.Split(',')))
        {
            string[] parts = metric.Split(';');
            string name = parts[0].Trim();
            double? duration = Duration(parts);
            if (duration is null)
            {
                continue;
            }

            if (name.Equals("cfRequestDuration", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("cfReqDur", StringComparison.OrdinalIgnoreCase))
            {
                requestDuration ??= duration;
            }
            else if (name.StartsWith("cfSpeed", StringComparison.OrdinalIgnoreCase))
            {
                speedParts += duration.Value;
            }
        }

        return requestDuration ?? (speedParts > 0 ? speedParts : DefaultServerMilliseconds);
    }

    /// <summary>Phase a request belongs to: empty downloads are latency probes.</summary>
    internal static SpeedTestStage StageOf(bool isDownload, long bytes) =>
        !isDownload ? SpeedTestStage.Upload : bytes == 0 ? SpeedTestStage.Latency : SpeedTestStage.Download;

    // The "dur=X" value of one Server-Timing metric, if it has one.
    private static double? Duration(string[] parts)
    {
        foreach (string part in parts.Skip(1))
        {
            string trimmed = part.Trim();
            if (trimmed.StartsWith("dur=", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(trimmed.AsSpan(4), NumberStyles.Float, CultureInfo.InvariantCulture, out double duration))
            {
                return duration;
            }
        }

        return null;
    }

    // Runs the connections for the phase time, reporting the speed of the last second, and returns the
    // average speed after the ramp-up. The last report of the phase is that result.
    private async Task<double> MeasureBandwidthAsync(Direction direction, IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken)
    {
        SpeedTestStage stage = direction == Direction.Download ? SpeedTestStage.Download : SpeedTestStage.Upload;
        progress?.Report(new SpeedTestProgress(stage, null, null));

        var counter = new TransferCounter();
        // Ends the phase: cuts the downloads and tells the uploads to stop sending.
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Drops uploads still waiting for the server's answer once the phase is over.
        using var drop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        phase.CancelAfter(_timing.PhaseDuration);
        long start = Stopwatch.GetTimestamp();
        Task[] connections = [.. Enumerable.Range(0, _timing.Connections).Select(_ => TransferAsync(direction, counter, phase, drop.Token, cancellationToken))];
        Task transfers = Task.WhenAll(connections);

        // Samples of (time, total bytes): the live window, the one taken when the ramp-up ended, and the latest.
        var window = new Queue<(TimeSpan At, long Bytes)>();
        window.Enqueue((TimeSpan.Zero, 0));
        (TimeSpan At, long Bytes)? rampUp = null;
        (TimeSpan At, long Bytes) latest = (TimeSpan.Zero, 0);

        try
        {
            while (true)
            {
                Task tick = Task.Delay(SampleInterval, cancellationToken);
                if (await Task.WhenAny(tick, transfers).ConfigureAwait(false) == transfers)
                {
                    break;
                }

                await tick.ConfigureAwait(false);
                if (phase.IsCancellationRequested)
                {
                    break;
                }

                latest = (Stopwatch.GetElapsedTime(start), counter.Bytes);
                if (rampUp is null && latest.At >= _timing.RampUp)
                {
                    rampUp = latest;
                }

                window.Enqueue(latest);
                if (window.Count > LiveWindowSamples + 1)
                {
                    window.Dequeue();
                }

                (TimeSpan At, long Bytes) oldest = window.Peek();
                progress?.Report(new SpeedTestProgress(stage, BitsPerSecond(latest.Bytes - oldest.Bytes, latest.At - oldest.At), null));
            }

            // The uploads finish their requests; one still without an answer after the timeout is dropped.
            drop.CancelAfter(UploadAnswerTimeout);
            // Rethrows a connection failure or the user's cancellation.
            await transfers.ConfigureAwait(false);
        }
        finally
        {
            await phase.CancelAsync().ConfigureAwait(false);
            drop.CancelAfter(UploadAnswerTimeout);
            // Waits for every connection to stop and observes its end, so nothing keeps running or goes unobserved.
            await transfers.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).ConfigureAwait(false);
        }

        if (rampUp is not { } measured || latest.At <= measured.At || latest.Bytes <= measured.Bytes)
        {
            throw new SpeedTestException(SpeedTestFailure.NoValidMeasurement);
        }

        double result = BitsPerSecond(latest.Bytes - measured.Bytes, latest.At - measured.At);
        progress?.Report(new SpeedTestProgress(stage, result, null));
        return result;
    }

    private static double BitsPerSecond(long bytes, TimeSpan elapsed) =>
        elapsed > TimeSpan.Zero ? bytes * 8 / elapsed.TotalSeconds : 0;

    // One connection: back-to-back requests until the phase ends. Downloads are cut by the phase;
    // uploads stop sending when it ends and are only dropped if the server doesn't answer in time.
    private async Task TransferAsync(Direction direction, TransferCounter counter, CancellationTokenSource phase, CancellationToken drop, CancellationToken cancellationToken)
    {
        try
        {
            while (!phase.IsCancellationRequested)
            {
                CancellationToken request = direction == Direction.Download ? phase.Token : drop;
                await MeasureAsync(direction, RequestBytes, counter, request, phase.Token).ConfigureAwait(false);
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Whatever an aborted request threw, the user's cancellation is what happened.
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception) when (phase.IsCancellationRequested)
        {
            // The phase is over: its time is up, or another connection failed and reports why.
        }
        catch (Exception)
        {
            // Stops the other connections; this failure is the one reported.
            await phase.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }

    // Runs one request and turns network exceptions into SpeedTestException with the phase and size.
    // Returns the latency (time to first byte minus server time) of downloads; 0 for uploads.
    // An upload stops sending when `stop` is cancelled; `cancellationToken` aborts the request.
    private async Task<double> MeasureAsync(Direction direction, long bytes, TransferCounter? counter, CancellationToken cancellationToken, CancellationToken stop)
    {
        bool isDownload = direction == Direction.Download;

        try
        {
            if (isDownload)
            {
                return await DownloadAsync(bytes, counter, cancellationToken).ConfigureAwait(false);
            }

            await UploadAsync(bytes, counter, stop, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (HttpRequestException exception)
        {
            throw new SpeedTestException(SpeedTestFailure.Unreachable, StageOf(isDownload, bytes), bytes, exception.Message.TrimEnd('.'), exception);
        }
        catch (IOException exception)
        {
            throw new SpeedTestException(SpeedTestFailure.ConnectionLost, StageOf(isDownload, bytes), bytes, exception.Message.TrimEnd('.'), exception);
        }
        catch (TaskCanceledException exception) when (exception.InnerException is TimeoutException)
        {
            throw new SpeedTestException(SpeedTestFailure.Timeout, StageOf(isDownload, bytes), bytes, innerException: exception);
        }
    }

    private static void EnsureAccepted(HttpResponseMessage response, bool isDownload, long bytes)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        SpeedTestFailure failure = response.StatusCode == HttpStatusCode.Forbidden
            ? SpeedTestFailure.Refused
            : SpeedTestFailure.ServerError;

        throw new SpeedTestException(failure, StageOf(isDownload, bytes), bytes, $"{(int)response.StatusCode} {response.ReasonPhrase}");
    }

    // Time to first byte minus the server time is the latency.
    private async Task<double> DownloadAsync(long bytes, TransferCounter? counter, CancellationToken cancellationToken)
    {
        var uri = new Uri($"{DownloadUrl}?bytes={bytes.ToString(CultureInfo.InvariantCulture)}");
        long start = Stopwatch.GetTimestamp();

        using HttpResponseMessage response = await _http
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        EnsureAccepted(response, isDownload: true, bytes);

        double firstByte = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        await DrainAsync(response, counter, cancellationToken).ConfigureAwait(false);

        return Math.Max(0, firstByte - ServerMilliseconds(response));
    }

    private async Task UploadAsync(long bytes, TransferCounter? counter, CancellationToken stop, CancellationToken cancellationToken)
    {
        using var content = new RandomContent(bytes, counter, stop);
        using HttpResponseMessage response = await _http
            .PostAsync(UploadUrl, content, cancellationToken)
            .ConfigureAwait(false);
        EnsureAccepted(response, isDownload: false, bytes);
    }

    private static async Task DrainAsync(HttpResponseMessage response, TransferCounter? counter, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920];

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            counter?.Add(read);
        }
    }

    // Bytes moved by all the connections of a phase; the sampler reads it while they add to it.
    private sealed class TransferCounter
    {
        private long _bytes;

        public long Bytes => Interlocked.Read(ref _bytes);

        public void Add(int bytes) => Interlocked.Add(ref _bytes, bytes);
    }

    // Upload body made of random bytes, so compression on the path can't inflate the result.
    // It's sent in chunks (no Content-Length), so it can end early as a complete request: it stops at its size
    // or when the phase ends. A write cut by an abort ends it quietly; the request then reports the abort.
    private sealed class RandomContent : HttpContent
    {
        // One random 64 KB block, reused so random data isn't generated for every chunk.
        private static readonly byte[] Block = RandomNumberGenerator.GetBytes(64 * 1024);

        private readonly long _maximumLength;
        private readonly TransferCounter? _counter;
        private readonly CancellationToken _stop;

        public RandomContent(long maximumLength, TransferCounter? counter, CancellationToken stop)
        {
            _maximumLength = maximumLength;
            _counter = counter;
            _stop = stop;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            long remaining = _maximumLength;

            try
            {
                while (remaining > 0 && !_stop.IsCancellationRequested)
                {
                    int chunk = (int)Math.Min(remaining, Block.Length);
                    await stream.WriteAsync(Block.AsMemory(0, chunk), cancellationToken).ConfigureAwait(false);
                    remaining -= chunk;
                    _counter?.Add(chunk);
                }
            }
            catch (Exception) when (_stop.IsCancellationRequested || cancellationToken.IsCancellationRequested)
            {
                // The connection was dropped because the phase ended or the user cancelled.
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

/// <summary>How long each bandwidth phase runs, how much of its start is left out, and how many connections it uses.</summary>
/// <param name="PhaseDuration">Length of the download phase, and of the upload phase.</param>
/// <param name="RampUp">Start of each phase that doesn't count, while TCP speeds up.</param>
/// <param name="Connections">Connections running at once.</param>
internal sealed record SpeedTestTiming(TimeSpan PhaseDuration, TimeSpan RampUp, int Connections)
{
    /// <summary>10 s per phase, the first 2 s left out, 4 connections like Ookla's standard test.</summary>
    public static SpeedTestTiming Default { get; } = new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), 4);
}
