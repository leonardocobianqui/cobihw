using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core.Network;

namespace CobiHw.App;

public sealed partial class SpeedTestViewModel : ObservableObject
{
    private readonly CloudflareSpeedTest _speedTest;
    private Func<string> _status;
    private Func<string> _live = () => string.Empty;

    public SpeedTestViewModel(CloudflareSpeedTest speedTest, SpeedTestResult? lastResult)
    {
        _speedTest = speedTest;
        LastResult = lastResult;
        _status = IdleText;
        StatusText = _status();
        Localizer.Instance.CultureChanged += (_, _) => RefreshTexts();
    }

    public event EventHandler<SpeedTestResult>? Completed;

    public event EventHandler<string>? Failed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(ResultText), nameof(LatencyText), nameof(MeasuredAtText))]
    private SpeedTestResult? _lastResult;

    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isTesting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveLabelText))]
    private string _liveText = string.Empty;

    public bool IsIdle => !IsTesting;

    public bool HasResult => LastResult is not null;

    public string LiveLabelText => Localizer.Format(Strings.Format_LiveTest, LiveText);

    public string ResultText => LastResult is null
        ? Strings.SpeedTest_NoneYet
        : $"↓ {DisplayFormat.Bandwidth(LastResult.DownloadBitsPerSecond)}   ↑ {DisplayFormat.Bandwidth(LastResult.UploadBitsPerSecond)}";

    public string LatencyText => LastResult is null
        ? string.Empty
        : Localizer.Format(
            Strings.Format_SpeedTestLatency,
            DisplayFormat.Milliseconds(LastResult.LatencyMilliseconds),
            DisplayFormat.Milliseconds(LastResult.JitterMilliseconds));

    public string MeasuredAtText => LastResult is null
        ? Strings.SpeedTest_HowTo
        : Localizer.Format(Strings.Format_MeasuredAt, Localizer.Format("{0:g}", LastResult.MeasuredAt.LocalDateTime));

    internal static string FailureText(SpeedTestException exception)
    {
        if (exception.Failure == SpeedTestFailure.NoValidMeasurement || exception.Stage is not SpeedTestStage stage)
        {
            return Strings.SpeedTest_FailedNoMeasurement;
        }

        string step = stage switch
        {
            SpeedTestStage.Latency => Strings.Step_Latency,
            SpeedTestStage.Download => Localizer.Format(Strings.Format_StepDownload, DisplayFormat.TransferSize(exception.Bytes)),
            _ => Localizer.Format(Strings.Format_StepUpload, DisplayFormat.TransferSize(exception.Bytes)),
        };

        string reason = exception.Failure switch
        {
            SpeedTestFailure.Refused => Localizer.Format(Strings.Format_ReasonRefused, exception.Detail),
            SpeedTestFailure.ServerError => Localizer.Format(Strings.Format_ReasonServerError, exception.Detail),
            SpeedTestFailure.Unreachable => Localizer.Format(Strings.Format_ReasonUnreachable, exception.Detail),
            SpeedTestFailure.ConnectionLost => Localizer.Format(Strings.Format_ReasonConnectionLost, exception.Detail),
            _ => Strings.Reason_Timeout,
        };

        return Localizer.Format(Strings.Format_TestFailed, step, reason);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var progress = new Progress<SpeedTestProgress>(ShowProgress);
        SetLive(() => Strings.SpeedTest_Starting);
        IsTesting = true;

        try
        {
            SpeedTestResult result = await _speedTest.RunAsync(progress, cancellationToken);
            LastResult = result;
            SetStatus(IdleText);
            Completed?.Invoke(this, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus(() => Strings.SpeedTest_Cancelled);
        }
        catch (SpeedTestException exception)
        {
            SetStatus(() => FailureText(exception));
            Failed?.Invoke(this, StatusText);
        }
        finally
        {
            IsTesting = false;
        }
    }

    private void ShowProgress(SpeedTestProgress progress)
    {
        if (!IsTesting)
        {
            return;
        }

        SetStatus(() => StageText(progress.Stage));
        SetLive(progress switch
        {
            { LatencyMilliseconds: double latency } => () => Localizer.Format(Strings.Format_LatencyLive, DisplayFormat.Milliseconds(latency)),
            { BitsPerSecond: double bits } => () => $"{Arrow(progress.Stage)} {DisplayFormat.Bandwidth(bits)}",
            _ => progress.Stage == SpeedTestStage.Latency
                ? () => Localizer.Format(Strings.Format_LatencyLive, DisplayFormat.Unavailable)
                : () => $"{Arrow(progress.Stage)} {DisplayFormat.Unavailable}",
        });
    }

    private void SetStatus(Func<string> status)
    {
        _status = status;
        StatusText = status();
    }

    private void SetLive(Func<string> live)
    {
        _live = live;
        LiveText = live();
    }

    private void RefreshTexts()
    {
        StatusText = _status();
        LiveText = _live();
        OnPropertyChanged(nameof(LiveLabelText));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(LatencyText));
        OnPropertyChanged(nameof(MeasuredAtText));
    }

    private string IdleText() => LastResult is null
        ? Strings.SpeedTest_NoneYetStatus
        : Localizer.Format(Strings.Format_LastTest, MeasuredAtText, ResultText, LatencyText);

    private static string StageText(SpeedTestStage stage) => stage switch
    {
        SpeedTestStage.Latency => Strings.Stage_Latency,
        SpeedTestStage.Download => Strings.Stage_Download,
        SpeedTestStage.Upload => Strings.Stage_Upload,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
    };

    private static string Arrow(SpeedTestStage stage) => stage == SpeedTestStage.Upload ? "↑" : "↓";
}
