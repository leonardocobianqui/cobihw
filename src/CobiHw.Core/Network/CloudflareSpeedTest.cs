using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;

namespace CobiHw.Core.Network;

/// <summary>
/// Internet speed test against speed.cloudflare.com, following the same method as Cloudflare's own page.
/// </summary>
/// <remarks>
/// Requests grow in size until one takes over a second, so slow connections stop early and fast ones
/// are measured with large transfers. Server processing time (Server-Timing header) is subtracted.
/// </remarks>
public sealed class CloudflareSpeedTest
{
    public const string DownloadUrl = "https://speed.cloudflare.com/__down";
    public const string UploadUrl = "https://speed.cloudflare.com/__up";

    // Latency = median of 20 empty downloads; bandwidth = 90th percentile of the samples.
    private const int LatencySamples = 20;
    private const double BandwidthPercentile = 0.9;
    private const double LatencyPercentile = 0.5;
    // Faster transfers are too short to measure reliably and are ignored.
    private const double MinRequestMilliseconds = 10;
    // A transfer this long is big enough: that direction is done.
    private const double FinishRequestMilliseconds = 1000;
    // Used when the response has no Server-Timing header.
    private const double DefaultServerMilliseconds = 10;

    // Same steps as speed.cloudflare.com: size in bytes and how many requests of that size.
    private static readonly BandwidthStep[] Plan =
    [
        new(Direction.Download, 100_000, 9),
        new(Direction.Download, 1_000_000, 8),
        new(Direction.Upload, 100_000, 8),
        new(Direction.Upload, 1_000_000, 6),
        new(Direction.Download, 10_000_000, 6),
        new(Direction.Upload, 10_000_000, 4),
        new(Direction.Download, 25_000_000, 4),
        new(Direction.Upload, 25_000_000, 4),
        new(Direction.Download, 50_000_000, 3),
        new(Direction.Upload, 50_000_000, 3),
        new(Direction.Download, 50_000_000, 2),
    ];

    private readonly HttpClient _http;

    /// <summary>Uses the given client, so tests can replace the network with a fake handler.</summary>
    public CloudflareSpeedTest(HttpClient http) => _http = http;

    private enum Direction
    {
        Download,
        Upload,
    }

    /// <summary>Runs latency, download and upload measurements.</summary>
    /// <param name="progress">Receives live updates; may be null.</param>
    /// <param name="cancellationToken">Stops the test.</param>
    /// <exception cref="SpeedTestException">The test couldn't complete.</exception>
    public async Task<SpeedTestResult> RunAsync(IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new SpeedTestProgress(SpeedTestStage.Latency, null, null));
        // Warm-up requests open the connection, so the first latency sample doesn't include the TLS handshake.
        await MeasureAsync(Direction.Download, 0, null, cancellationToken).ConfigureAwait(false);
        await MeasureAsync(Direction.Download, 100_000, null, cancellationToken).ConfigureAwait(false);

        var latencies = new List<double>(LatencySamples);
        for (int i = 0; i < LatencySamples; i++)
        {
            Sample sample = await MeasureAsync(Direction.Download, 0, null, cancellationToken).ConfigureAwait(false);
            latencies.Add(sample.FirstByteMilliseconds);
            progress?.Report(new SpeedTestProgress(SpeedTestStage.Latency, null, sample.FirstByteMilliseconds));
        }

        var downloads = new List<double>();
        var uploads = new List<double>();
        bool downloadFinished = false;
        bool uploadFinished = false;
        SpeedTestStage? currentStage = null;

        foreach (BandwidthStep step in Plan)
        {
            bool isDownload = step.Direction == Direction.Download;
            if (isDownload ? downloadFinished : uploadFinished)
            {
                continue;
            }

            SpeedTestStage stage = isDownload ? SpeedTestStage.Download : SpeedTestStage.Upload;
            if (stage != currentStage)
            {
                currentStage = stage;
                progress?.Report(new SpeedTestProgress(stage, null, null));
            }

            Action<double>? onLive = progress is null
                ? null
                : bitsPerSecond => progress.Report(new SpeedTestProgress(stage, bitsPerSecond, null));

            for (int i = 0; i < step.Count; i++)
            {
                Sample sample = await MeasureAsync(step.Direction, step.Bytes, onLive, cancellationToken).ConfigureAwait(false);

                if (sample.TransferMilliseconds >= MinRequestMilliseconds)
                {
                    (isDownload ? downloads : uploads).Add(sample.BitsPerSecond);
                    onLive?.Invoke(sample.BitsPerSecond);
                }

                if (sample.TransferMilliseconds >= FinishRequestMilliseconds)
                {
                    if (isDownload)
                    {
                        downloadFinished = true;
                    }
                    else
                    {
                        uploadFinished = true;
                    }

                    break;
                }
            }
        }

        return new SpeedTestResult(
            DateTimeOffset.Now,
            Percentile(downloads, BandwidthPercentile),
            Percentile(uploads, BandwidthPercentile),
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

    /// <summary>Server processing time from the "Server-Timing: …;dur=X" header.</summary>
    internal static double ServerMilliseconds(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Server-Timing", out IEnumerable<string>? values))
        {
            return DefaultServerMilliseconds;
        }

        foreach (string part in values.SelectMany(value => value.Split(';', ',')))
        {
            string trimmed = part.Trim();
            if (trimmed.StartsWith("dur=", StringComparison.OrdinalIgnoreCase) &&
                double.TryParse(trimmed.AsSpan(4), NumberStyles.Float, CultureInfo.InvariantCulture, out double duration))
            {
                return duration;
            }
        }

        return DefaultServerMilliseconds;
    }

    /// <summary>Phase a request belongs to: empty downloads are latency probes.</summary>
    internal static SpeedTestStage StageOf(bool isDownload, long bytes) =>
        !isDownload ? SpeedTestStage.Upload : bytes == 0 ? SpeedTestStage.Latency : SpeedTestStage.Download;

    // Runs one request and turns network exceptions into SpeedTestException with the phase and size.
    private async Task<Sample> MeasureAsync(Direction direction, long bytes, Action<double>? onLive, CancellationToken cancellationToken)
    {
        bool isDownload = direction == Direction.Download;

        try
        {
            return isDownload
                ? await DownloadAsync(bytes, onLive, cancellationToken).ConfigureAwait(false)
                : await UploadAsync(bytes, onLive, cancellationToken).ConfigureAwait(false);
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

    // Time to first byte is the latency; total time minus server time is the transfer time.
    private async Task<Sample> DownloadAsync(long bytes, Action<double>? onLive, CancellationToken cancellationToken)
    {
        var uri = new Uri($"{DownloadUrl}?bytes={bytes.ToString(CultureInfo.InvariantCulture)}");
        long start = Stopwatch.GetTimestamp();

        using HttpResponseMessage response = await _http
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        EnsureAccepted(response, isDownload: true, bytes);

        double firstByte = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long received = await DrainAsync(response, new LiveMeter(onLive), cancellationToken).ConfigureAwait(false);
        double total = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        double server = ServerMilliseconds(response);

        return new Sample(received, total - server, Math.Max(0, firstByte - server));
    }

    // Uploads have no separate first byte, so latency is the whole transfer.
    private async Task<Sample> UploadAsync(long bytes, Action<double>? onLive, CancellationToken cancellationToken)
    {
        using var content = new RandomContent(bytes, new LiveMeter(onLive));
        long start = Stopwatch.GetTimestamp();

        using HttpResponseMessage response = await _http
            .PostAsync(UploadUrl, content, cancellationToken)
            .ConfigureAwait(false);
        EnsureAccepted(response, isDownload: false, bytes);

        double total = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        double server = ServerMilliseconds(response);

        return new Sample(bytes, total - server, Math.Max(0, total - server));
    }

    private static async Task<long> DrainAsync(HttpResponseMessage response, LiveMeter meter, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920];
        long received = 0;

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            received += read;
            meter.Add(read);
        }

        return received;
    }

    // One step of the plan.
    private readonly record struct BandwidthStep(Direction Direction, long Bytes, int Count);

    // One request's measurement.
    private readonly record struct Sample(long Bytes, double TransferMilliseconds, double FirstByteMilliseconds)
    {
        public double BitsPerSecond => Bytes * 8 / (TransferMilliseconds / 1000);
    }

    // Reports the live speed at most every 250 ms while a transfer runs.
    private sealed class LiveMeter
    {
        private const double ReportMilliseconds = 250;

        private readonly Action<double>? _onLive;
        private long _start = Stopwatch.GetTimestamp();
        private long _bytes;

        public LiveMeter(Action<double>? onLive) => _onLive = onLive;

        public void Add(int bytes)
        {
            if (_onLive is null)
            {
                return;
            }

            _bytes += bytes;
            TimeSpan elapsed = Stopwatch.GetElapsedTime(_start);
            if (elapsed.TotalMilliseconds < ReportMilliseconds)
            {
                return;
            }

            _onLive(_bytes * 8 / elapsed.TotalSeconds);
            _bytes = 0;
            _start = Stopwatch.GetTimestamp();
        }
    }

    // Upload body made of random bytes, so compression on the path can't inflate the result.
    private sealed class RandomContent : HttpContent
    {
        // One random 64 KB block, reused so random data isn't generated for every chunk.
        private static readonly byte[] Block = RandomNumberGenerator.GetBytes(64 * 1024);

        private readonly long _length;
        private readonly LiveMeter _meter;

        public RandomContent(long length, LiveMeter meter)
        {
            _length = length;
            _meter = meter;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            long remaining = _length;

            while (remaining > 0)
            {
                int chunk = (int)Math.Min(remaining, Block.Length);
                await stream.WriteAsync(Block.AsMemory(0, chunk), cancellationToken).ConfigureAwait(false);
                remaining -= chunk;
                _meter.Add(chunk);
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override bool TryComputeLength(out long length)
        {
            length = _length;
            return true;
        }
    }
}
