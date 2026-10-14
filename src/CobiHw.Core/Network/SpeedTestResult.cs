namespace CobiHw.Core.Network;

public enum SpeedTestStage
{
    Latency,
    Download,
    Upload,
}

public sealed record SpeedTestProgress(SpeedTestStage Stage, double? BitsPerSecond, double? LatencyMilliseconds);

public sealed record SpeedTestResult(
    DateTimeOffset MeasuredAt,
    double DownloadBitsPerSecond,
    double UploadBitsPerSecond,
    double LatencyMilliseconds,
    double JitterMilliseconds);

public enum SpeedTestFailure
{
    NoValidMeasurement,
    Refused,
    ServerError,
    Unreachable,
    ConnectionLost,
    Timeout,
}

public sealed class SpeedTestException : Exception
{
    public SpeedTestException(SpeedTestFailure failure, SpeedTestStage? stage = null, long bytes = 0, string? detail = null, Exception? innerException = null)
        : base(BuildMessage(failure, stage, bytes, detail), innerException)
    {
        Failure = failure;
        Stage = stage;
        Bytes = bytes;
        Detail = detail;
    }

    public SpeedTestFailure Failure { get; }

    public SpeedTestStage? Stage { get; }

    public long Bytes { get; }

    public string? Detail { get; }

    private static string BuildMessage(SpeedTestFailure failure, SpeedTestStage? stage, long bytes, string? detail)
    {
        string where = stage is null ? string.Empty : $" during {stage} ({bytes} bytes)";
        string why = detail is null ? string.Empty : $": {detail}";
        return $"Speed test failed{where}: {failure}{why}";
    }
}
