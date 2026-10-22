using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Fps;

/// <summary>
/// Measures the foreground app's frame rate from DXGI and Direct3D 9 "Present" ETW events, like PresentMon.
/// </summary>
/// <remarks>Starting an ETW session requires administrator rights; the app gets them through a scheduled task.</remarks>
public sealed class FpsMonitor : IDisposable
{
    // One session per Windows logon session, so two users can each run cobihw.
    private const string SessionPrefix = "cobihw-fps-";

    // DXGI covers Direct3D 10–12; D3D9 covers older games.
    private static readonly string[] Providers = ["Microsoft-Windows-DXGI", "Microsoft-Windows-D3D9"];
    private static readonly string[] PresentTasks = ["Present", "PresentMultiplaneOverlay"];

    private readonly FrameCounter _counter = new();
    private readonly TraceEventSession _session;
    private readonly Thread _processing;
    private int _lastProcessId;
    private string? _lastProcessName;

    private FpsMonitor(TraceEventSession session)
    {
        _session = session;
        _session.Source.Dynamic.All += OnEvent;
        _processing = new Thread(() => _session.Source.Process()) { IsBackground = true, Name = "cobihw FPS" };
        _processing.Start();
    }

    /// <summary>Starts the ETW session and the thread that processes its events.</summary>
    /// <exception cref="UnauthorizedAccessException">The process isn't elevated.</exception>
    public static FpsMonitor Start()
    {
        using Process current = Process.GetCurrentProcess();
        var session = new TraceEventSession(SessionPrefix + current.SessionId) { StopOnDispose = true };

        try
        {
            foreach (string provider in Providers)
            {
                session.EnableProvider(provider, TraceEventLevel.Informational);
            }

            return new FpsMonitor(session);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>Frame rate of the app the user is looking at.</summary>
    /// <returns>The rate, or <see langword="null"/> when the foreground app isn't drawing frames (or is cobihw itself).</returns>
    public FpsSnapshot? Sample()
    {
        int processId = ForegroundProcessId();

        if (processId == 0 || processId == Environment.ProcessId)
        {
            return null;
        }

        double? rate = _counter.Rate(processId, Clock(Stopwatch.GetTimestamp()));

        return rate is null ? null : new FpsSnapshot(rate.Value, ProcessName(processId));
    }

    /// <summary>Stops the ETW session and waits for the processing thread to finish.</summary>
    public void Dispose()
    {
        _session.Dispose();
        _processing.Join();
    }

    // Runs on the ETW thread for every event; only the start of a Present call counts as a frame.
    private void OnEvent(TraceEvent data)
    {
        if (data.Opcode != TraceEventOpcode.Start ||
            !PresentTasks.Contains(data.TaskName) ||
            !Providers.Contains(data.ProviderName))
        {
            return;
        }

// TimeStampQPC is marked obsolete but is the raw QPC value needed to compare with Stopwatch.
#pragma warning disable CS0618
        _counter.Record(data.ProcessID, Clock(data.TimeStampQPC));
#pragma warning restore CS0618
    }

    private static TimeSpan Clock(long timestamp) => Stopwatch.GetElapsedTime(0, timestamp);

    private static int ForegroundProcessId()
    {
        nint window = User32.GetForegroundWindow();

        if (window == 0)
        {
            return 0;
        }

        User32.GetWindowThreadProcessId(window, out uint processId);
        return (int)processId;
    }

    // Caches the last name, so the process isn't opened every second.
    private string? ProcessName(int processId)
    {
        if (processId != _lastProcessId)
        {
            _lastProcessId = processId;
            _lastProcessName = ReadProcessName(processId);
        }

        return _lastProcessName;
    }

    private static string? ReadProcessName(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
