namespace CobiHw.Core.Fps;

/// <summary>
/// Counts presented frames per process over a sliding one-second window.
/// </summary>
/// <remarks>Thread-safe: frames arrive on the ETW thread and are read on the UI thread.</remarks>
public sealed class FrameCounter
{
    /// <summary>Length of the sliding window.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(1);
    /// <summary>Processes with no frames for this long are forgotten.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(4);

    private readonly Lock _lock = new();
    private readonly Dictionary<int, ProcessFrames> _processes = [];

    /// <summary>Records one presented frame.</summary>
    public void Record(int processId, TimeSpan presentedAt)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(processId, out ProcessFrames? frames))
            {
                frames = new ProcessFrames();
                _processes.Add(processId, frames);
            }

            frames.Add(presentedAt);
        }
    }

    /// <summary>Frames per second of a process, measured between the oldest and newest frame in the window.</summary>
    /// <returns>The rate, or <see langword="null"/> when the process isn't presenting frames.</returns>
    public double? Rate(int processId, TimeSpan now)
    {
        lock (_lock)
        {
            RemoveStale(now);

            if (!_processes.TryGetValue(processId, out ProcessFrames? frames))
            {
                return null;
            }

            return frames.Rate();
        }
    }

    private void RemoveStale(TimeSpan now)
    {
        foreach (KeyValuePair<int, ProcessFrames> process in _processes)
        {
            if (now - process.Value.Newest > StaleAfter)
            {
                _processes.Remove(process.Key);
            }
        }
    }

    // Frame times of one process, oldest first.
    private sealed class ProcessFrames
    {
        private readonly Queue<TimeSpan> _times = new();

        public TimeSpan Newest { get; private set; }

        public void Add(TimeSpan presentedAt)
        {
            _times.Enqueue(presentedAt);

            if (presentedAt > Newest)
            {
                Newest = presentedAt;
            }

            // Drops frames that fell out of the window.
            while (Newest - _times.Peek() > Window)
            {
                _times.Dequeue();
            }
        }

        public double? Rate()
        {
            if (_times.Count < 2)
            {
                return null;
            }

            // Min() because ETW can deliver events slightly out of order.
            TimeSpan span = Newest - _times.Min();

            return span > TimeSpan.Zero ? (_times.Count - 1) / span.TotalSeconds : null;
        }
    }
}
