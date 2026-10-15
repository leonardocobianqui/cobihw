namespace CobiHw.Core.Fps;

public sealed class FrameCounter
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(4);

    private readonly Lock _lock = new();
    private readonly Dictionary<int, ProcessFrames> _processes = [];

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

            TimeSpan span = Newest - _times.Min();

            return span > TimeSpan.Zero ? (_times.Count - 1) / span.TotalSeconds : null;
        }
    }
}
