namespace CobiHw.App;

internal sealed class SingleInstance : IDisposable
{
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

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (createdNew)
        {
            return new SingleInstance(mutex, showEvent);
        }

        showEvent.Set();
        showEvent.Dispose();
        mutex.Dispose();
        return null;
    }

    public void ListenForShowRequests(Action onShowRequested)
    {
        _showRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => onShowRequested(),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _showRegistration?.Unregister(null);
        _showEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
