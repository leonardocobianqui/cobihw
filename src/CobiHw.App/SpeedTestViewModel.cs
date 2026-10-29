using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core.Network;

namespace CobiHw.App;

/// <summary>Runs the Cloudflare speed test and shows its progress and last result on the widget, in the speed test window, in the settings window and in the tray.</summary>
public sealed partial class SpeedTestViewModel : ObservableObject
{
    private readonly CloudflareSpeedTest _speedTest;
    // Texts are kept as functions so a language change can rebuild them.
    private Func<string> _status;
    private Func<string> _live = () => string.Empty;
    // Latest value of each phase of the current run (indexed by SpeedTestStage) and the ping jitter.
    // The last progress of a phase is its result, so a finished phase holds its result here.
    private readonly double?[] _results = new double?[3];
    private double? _jitter;
    // Phase running now; null before the first update.
    private SpeedTestStage? _stage;
    // True once the current run ended with a result.
    private bool _succeeded;

    /// <summary>Creates the test with the result saved from the last run.</summary>
    /// <param name="speedTest">Client that runs the test.</param>
    /// <param name="lastResult">Saved result, or null.</param>
    public SpeedTestViewModel(CloudflareSpeedTest speedTest, SpeedTestResult? lastResult)
    {
        _speedTest = speedTest;
        LastResult = lastResult;
        _status = IdleText;
        StatusText = _status();
        Localizer.Instance.CultureChanged += (_, _) => RefreshTexts();
    }

    /// <summary>Raised with the result of a successful test.</summary>
    public event EventHandler<SpeedTestResult>? Completed;

    /// <summary>Raised with the translated error message.</summary>
    public event EventHandler<string>? Failed;

    /// <summary>Latest result, or null when no test has run yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(ResultText), nameof(LatencyText), nameof(MeasuredAtText))]
    private SpeedTestResult? _lastResult;

    /// <summary>Status line for the settings window.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowStatusText))]
    private string _statusText = string.Empty;

    /// <summary>True while a test runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isTesting;

    /// <summary>Current step and speed while the test runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveLabelText))]
    private string _liveText = string.Empty;

    /// <summary>Speed under the gauge needle, in Mbps: the running phase's live speed, then the download of a finished test.</summary>
    [ObservableProperty] private double _gaugeMegabits;

    /// <summary>True when no test is running.</summary>
    public bool IsIdle => !IsTesting;

    /// <summary>Ping, download and upload: the steps and cards of the speed test window.</summary>
    public IReadOnlyList<SpeedTestPhase> Phases { get; } =
        [new(SpeedTestStage.Latency), new(SpeedTestStage.Download), new(SpeedTestStage.Upload)];

    /// <summary>True while the upload runs, so the gauge takes the upload color.</summary>
    public bool IsUploading => IsTesting && _stage == SpeedTestStage.Upload;

    /// <summary>Number in the middle of the gauge.</summary>
    public string GaugeValueText => GaugeReading().Number;

    /// <summary>Unit and phase below the gauge number, e.g. "Mbps · Download".</summary>
    public string GaugeUnitText => GaugeReading().Unit;

    /// <summary>Status line of the speed test window: the current step, the error, or when the result was measured.</summary>
    public string WindowStatusText => _succeeded && !IsTesting ? MeasuredAtText : StatusText;

    /// <summary>True when there is a result to show.</summary>
    public bool HasResult => LastResult is not null;

    /// <summary>Live text with the "Test" label, for the widget.</summary>
    public string LiveLabelText => Localizer.Format(Strings.Format_LiveTest, LiveText);

    /// <summary>Download and upload of the last result.</summary>
    public string ResultText => LastResult is null
        ? Strings.SpeedTest_NoneYet
        : DisplayFormat.UpDown(DisplayFormat.Bandwidth(LastResult.DownloadBitsPerSecond), DisplayFormat.Bandwidth(LastResult.UploadBitsPerSecond));

    /// <summary>Latency and jitter of the last result.</summary>
    public string LatencyText => LastResult is null
        ? string.Empty
        : Localizer.Format(
            Strings.Format_SpeedTestLatency,
            DisplayFormat.Milliseconds(LastResult.LatencyMilliseconds),
            DisplayFormat.Milliseconds(LastResult.JitterMilliseconds));

    /// <summary>When the last test ran, or how to start one.</summary>
    public string MeasuredAtText => LastResult is null
        ? Strings.SpeedTest_HowTo
        : Localizer.Format(Strings.Format_MeasuredAt, Localizer.Format("{0:g}", LastResult.MeasuredAt.LocalDateTime));

    /// <summary>Explains a failure: the step (with how much was transferred) and the reason.</summary>
    /// <param name="exception">Speed test failure.</param>
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

    /// <summary>Runs the test; the toolkit generates RunCommand and RunCancelCommand from it.</summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var progress = new Progress<SpeedTestProgress>(ShowProgress);
        SetLive(() => Strings.SpeedTest_Starting);
        Array.Clear(_results);
        _jitter = null;
        _stage = null;
        _succeeded = false;
        IsTesting = true;
        UpdateRun();

        try
        {
            SpeedTestResult result = await _speedTest.RunAsync(progress, cancellationToken);
            LastResult = result;
            ShowResult(result);
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
            UpdateRun();
        }
    }

    // Progress can still arrive after the test ends; it is ignored then.
    private void ShowProgress(SpeedTestProgress progress)
    {
        if (!IsTesting)
        {
            return;
        }

        _stage = progress.Stage;
        if (progress.LatencyMilliseconds is double latencySoFar)
        {
            _results[(int)progress.Stage] = latencySoFar;
            _jitter = progress.JitterMilliseconds;
        }
        else if (progress.BitsPerSecond is double bitsSoFar)
        {
            _results[(int)progress.Stage] = bitsSoFar;
        }

        UpdateRun();
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
        foreach (SpeedTestPhase phase in Phases)
        {
            phase.RefreshTexts();
        }

        OnPropertyChanged(nameof(GaugeValueText));
        OnPropertyChanged(nameof(GaugeUnitText));
        OnPropertyChanged(nameof(WindowStatusText));
    }

    // The final result fills every card; it matches the last progress of each phase.
    private void ShowResult(SpeedTestResult result)
    {
        _results[(int)SpeedTestStage.Latency] = result.LatencyMilliseconds;
        _results[(int)SpeedTestStage.Download] = result.DownloadBitsPerSecond;
        _results[(int)SpeedTestStage.Upload] = result.UploadBitsPerSecond;
        _jitter = result.JitterMilliseconds;
        _succeeded = true;
    }

    // Refreshes the speed test window: phases before the running one are done, with their results.
    private void UpdateRun()
    {
        int running = _stage is SpeedTestStage stage ? (int)stage : -1;
        foreach (SpeedTestPhase phase in Phases)
        {
            int index = (int)phase.Stage;
            SpeedTestPhaseState state = _succeeded || index < running ? SpeedTestPhaseState.Done
                : index == running && IsTesting ? SpeedTestPhaseState.Active
                : SpeedTestPhaseState.Pending;
            phase.Show(state, _results[index], phase.Stage == SpeedTestStage.Latency ? _jitter : null);
        }

        GaugeMegabits = (GaugeBitsPerSecond() ?? 0) / 1_000_000;
        OnPropertyChanged(nameof(IsUploading));
        OnPropertyChanged(nameof(GaugeValueText));
        OnPropertyChanged(nameof(GaugeUnitText));
        OnPropertyChanged(nameof(WindowStatusText));
    }

    // Speed for the gauge: the running phase's live speed, or the download of a finished test.
    // Null while the ping is measured, and after a test that didn't finish.
    private double? GaugeBitsPerSecond()
    {
        if (!IsTesting)
        {
            return _succeeded ? _results[(int)SpeedTestStage.Download] : null;
        }

        return _stage is SpeedTestStage stage && stage != SpeedTestStage.Latency ? _results[(int)stage] : null;
    }

    // Number and unit in the middle of the gauge: milliseconds while the ping runs, otherwise the speed.
    private (string Number, string Unit) GaugeReading()
    {
        if (IsTesting && _stage == SpeedTestStage.Latency)
        {
            string unit = Localizer.Format(Strings.Format_GaugeUnit, Strings.Unit_Milliseconds, Strings.Network_Ping);
            return _results[(int)SpeedTestStage.Latency] is double latency
                ? (Localizer.Format("{0:0}", latency), unit)
                : (DisplayFormat.Unavailable, unit);
        }

        string phase = IsUploading ? Strings.SpeedTest_Upload : Strings.SpeedTest_Download;
        return GaugeBitsPerSecond() is double bitsPerSecond
            ? (DisplayFormat.BandwidthNumber(bitsPerSecond), Localizer.Format(Strings.Format_GaugeUnit, DisplayFormat.BandwidthUnit(bitsPerSecond), phase))
            : (DisplayFormat.Unavailable, Localizer.Format(Strings.Format_GaugeUnit, Strings.Unit_Megabits, phase));
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
