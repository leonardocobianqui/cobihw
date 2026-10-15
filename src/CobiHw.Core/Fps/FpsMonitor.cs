using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Fps;

public sealed class FpsMonitor : IDisposable
{
    private const string SessionName = "cobihw-fps";

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

    public static FpsMonitor Start()
    {
        var session = new TraceEventSession(SessionName) { StopOnDispose = true };

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

    public void Dispose()
    {
        _session.Dispose();
        _processing.Join();
    }

    private void OnEvent(TraceEvent data)
    {
        if (data.Opcode != TraceEventOpcode.Start ||
            !PresentTasks.Contains(data.TaskName) ||
            !Providers.Contains(data.ProviderName))
        {
            return;
        }

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
