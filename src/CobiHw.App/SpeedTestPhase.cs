using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core.Network;

namespace CobiHw.App;

/// <summary>Where a phase is in the current test.</summary>
public enum SpeedTestPhaseState
{
    /// <summary>Not reached yet, or the test stopped before it ended.</summary>
    Pending,
    /// <summary>Running now.</summary>
    Active,
    /// <summary>Finished, with its result.</summary>
    Done,
}

/// <summary>One phase of the speed test (ping, download or upload): its step and its card in the speed test window.</summary>
public sealed partial class SpeedTestPhase : ObservableObject
{
    // Result kept as a number, so the texts follow a language change.
    private double? _value;
    private double? _jitter;

    /// <summary>Creates a pending phase.</summary>
    /// <param name="stage">Phase of the test.</param>
    public SpeedTestPhase(SpeedTestStage stage) => Stage = stage;

    /// <summary>Phase of the test.</summary>
    public SpeedTestStage Stage { get; }

    /// <summary>True for the upload, which has its own color.</summary>
    public bool IsUpload => Stage == SpeedTestStage.Upload;

    /// <summary>Not reached, running or finished.</summary>
    [ObservableProperty] private SpeedTestPhaseState _state;

    /// <summary>Step and card title.</summary>
    public string Name => Stage switch
    {
        SpeedTestStage.Latency => Strings.Network_Ping,
        SpeedTestStage.Download => Strings.SpeedTest_Download,
        _ => Strings.SpeedTest_Upload,
    };

    /// <summary>Result once the phase is done: "21 ms" or "312 Mbps"; "—" before that.</summary>
    public string ValueText => _value switch
    {
        null => DisplayFormat.Unavailable,
        double latency when Stage == SpeedTestStage.Latency => DisplayFormat.Milliseconds(latency),
        double bitsPerSecond => DisplayFormat.Bandwidth(bitsPerSecond),
    };

    /// <summary>Jitter under the ping result; empty otherwise.</summary>
    public string DetailText => _jitter is double jitter
        ? Localizer.Format(Strings.Format_Jitter, DisplayFormat.Milliseconds(jitter))
        : string.Empty;

    /// <summary>Updates the state; the result shows only when the phase is done.</summary>
    /// <param name="state">New state.</param>
    /// <param name="value">Latency in ms or speed in bits/s.</param>
    /// <param name="jitter">Jitter in ms, for the ping.</param>
    internal void Show(SpeedTestPhaseState state, double? value, double? jitter)
    {
        State = state;
        _value = state == SpeedTestPhaseState.Done ? value : null;
        _jitter = state == SpeedTestPhaseState.Done ? jitter : null;
        RefreshTexts();
    }

    /// <summary>Rebuilds the texts after a language change.</summary>
    internal void RefreshTexts()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(DetailText));
    }
}
