using System.Collections.Concurrent;

namespace CobiHw.Tests;

/// <summary>Runs test code the way the app's UI thread runs it: one thread, callbacks in the order they were posted.</summary>
/// <remarks>
/// Without it, <see cref="Progress{T}"/> reports and await continuations run on the thread pool, in any order and at
/// the same time, so a late progress report could overwrite the final texts. The WPF dispatcher never does that.
/// </remarks>
internal static class UiThread
{
    /// <summary>Runs <paramref name="body"/> on a new thread with a callback queue, until its task ends.</summary>
    /// <param name="body">Test code; awaits inside it come back to the same thread.</param>
    public static Task RunAsync(Func<Task> body)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var queue = new QueueContext();
            SynchronizationContext.SetSynchronizationContext(queue);

            try
            {
                Task task = body();
                // Callbacks posted before the end are already in the queue, so they run before the stop.
                task.ContinueWith(_ => queue.Stop(), TaskScheduler.Default);
                queue.Run();
                task.GetAwaiter().GetResult();
                finished.SetResult();
            }
            catch (Exception exception)
            {
                finished.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "UI thread (test)",
        };

        thread.Start();
        return finished.Task;
    }

    // Runs posted callbacks one at a time, in order, on the thread that calls Run.
    private sealed class QueueContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
        private bool _stopped;

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

        public override SynchronizationContext CreateCopy() => this;

        // Queued like any callback, so everything posted before it still runs.
        public void Stop() => _queue.Add((_ => _stopped = true, null));

        public void Run()
        {
            while (!_stopped)
            {
                (SendOrPostCallback callback, object? state) = _queue.Take();
                callback(state);
            }
        }
    }
}
