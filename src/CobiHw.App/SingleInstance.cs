namespace CobiHw.App;

/// <summary>
/// Keeps one copy of the app per user session.
/// A second launch signals the first one to show its widget, then exits.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    // The Local prefix scopes both names to the current Windows session.
    private const string MutexName = @"Local\cobihw.SingleInstance";
    private const string ShowEventName = @"Local\cobihw.Show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private RegisteredWaitHandle? _showRegistration;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent)
    {
        _mutex = mutex;
        _showEvent = showEvent;
    }

    /// <summary>Becomes the running instance, or signals the existing one and returns null.</summary>
    public static SingleInstance? TryAcquire()
    {
        Mutex mutex;

        try
        {
            mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                SignalRunningInstance();
                mutex.Dispose();
                return null;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName));
    }

    // The other instance may be closing; then there is nothing to signal.
    private static void SignalRunningInstance()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
            showEvent.Set();
        }
        catch (Exception exception) when (exception is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Calls <paramref name="onShowRequested"/> on a pool thread every time another launch signals.</summary>
    /// <param name="onShowRequested">Action to run; it must switch to the UI thread itself.</param>
    public void ListenForShowRequests(Action onShowRequested)
    {
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => onShowRequested(),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>Stops listening and releases the lock.</summary>
    public void Dispose()
    {
        _showRegistration?.Unregister(null);
        _showEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
