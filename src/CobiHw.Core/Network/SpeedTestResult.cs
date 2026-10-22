namespace CobiHw.Core.Network;

/// <summary>Phase of the speed test, in the order they run.</summary>
public enum SpeedTestStage
{
    Latency,
    Download,
    Upload,
}

/// <summary>Progress update for the live display.</summary>
/// <param name="Stage">Current phase.</param>
/// <param name="BitsPerSecond">Live speed, when measuring bandwidth.</param>
/// <param name="LatencyMilliseconds">Latest latency sample, when measuring latency.</param>
public sealed record SpeedTestProgress(SpeedTestStage Stage, double? BitsPerSecond, double? LatencyMilliseconds);

/// <summary>Final speed test result.</summary>
/// <param name="MeasuredAt">When the test finished.</param>
/// <param name="DownloadBitsPerSecond">90th percentile of the download samples.</param>
/// <param name="UploadBitsPerSecond">90th percentile of the upload samples.</param>
/// <param name="LatencyMilliseconds">Median latency.</param>
/// <param name="JitterMilliseconds">Average difference between consecutive latency samples.</param>
public sealed record SpeedTestResult(
    DateTimeOffset MeasuredAt,
    double DownloadBitsPerSecond,
    double UploadBitsPerSecond,
    double LatencyMilliseconds,
    double JitterMilliseconds);

/// <summary>Why a speed test failed; each value has its own message in the UI.</summary>
public enum SpeedTestFailure
{
    /// <summary>Every sample was too fast to measure.</summary>
    NoValidMeasurement,
    /// <summary>HTTP 403: Cloudflare refused the request (usually rate limiting).</summary>
    Refused,
    /// <summary>Any other HTTP error status.</summary>
    ServerError,
    /// <summary>DNS or connection failure.</summary>
    Unreachable,
    /// <summary>The connection dropped mid-transfer.</summary>
    ConnectionLost,
    /// <summary>The HTTP client timed out.</summary>
    Timeout,
}

/// <summary>Speed test failure with enough context for a user-facing message.</summary>
public sealed class SpeedTestException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="failure">Kind of failure.</param>
    /// <param name="stage">Phase that failed, if known.</param>
    /// <param name="bytes">Size of the request that failed.</param>
    /// <param name="detail">Extra detail, e.g. "403 Forbidden".</param>
    /// <param name="innerException">Original exception.</param>
    public SpeedTestException(SpeedTestFailure failure, SpeedTestStage? stage = null, long bytes = 0, string? detail = null, Exception? innerException = null)
        : base(BuildMessage(failure, stage, bytes, detail), innerException)
    {
        Failure = failure;
        Stage = stage;
        Bytes = bytes;
        Detail = detail;
    }

    /// <summary>Kind of failure.</summary>
    public SpeedTestFailure Failure { get; }

    /// <summary>Phase that failed, if known.</summary>
    public SpeedTestStage? Stage { get; }

    /// <summary>Size of the request that failed.</summary>
    public long Bytes { get; }

    /// <summary>Extra detail for the message.</summary>
    public string? Detail { get; }

    private static string BuildMessage(SpeedTestFailure failure, SpeedTestStage? stage, long bytes, string? detail)
    {
        string where = stage is null ? string.Empty : $" during {stage} ({bytes} bytes)";
        string why = detail is null ? string.Empty : $": {detail}";
        return $"Speed test failed{where}: {failure}{why}";
    }
}
