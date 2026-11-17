using System.Net.Sockets;
using System.Text;

namespace CobiHw.App.Linux;

/// <summary>
/// One running app per user: the first instance listens on a Unix socket, and a second launch asks it to show the widget.
/// </summary>
/// <remarks>
/// The socket lives in $XDG_RUNTIME_DIR, a folder only this user can open, which the session removes at logout.
/// A socket left behind by a crash is detected (nobody answers) and replaced.
/// </remarks>
internal sealed class SingleInstance : IDisposable
{
    private const string ShowRequest = "show";

    private readonly Socket _listener;
    private readonly string _path;

    private SingleInstance(Socket listener, string path)
    {
        _listener = listener;
        _path = path;
    }

    /// <summary>Takes the instance, or returns null after asking the running one to show its widget.</summary>
    public static SingleInstance? TryAcquire()
    {
        string folder = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtime ? runtime : Path.GetTempPath();
        string path = Path.Combine(folder, $"cobihw-{Environment.UserName}.sock");
        var endPoint = new UnixDomainSocketEndPoint(path);

        if (File.Exists(path))
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                client.Connect(endPoint);
                client.Send(Encoding.ASCII.GetBytes(ShowRequest));
                return null;
            }
            catch (SocketException)
            {
                // Nobody listening: a previous run crashed. The stale socket is replaced.
                File.Delete(path);
            }
        }

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(endPoint);
            listener.Listen();
            return new SingleInstance(listener, path);
        }
        catch (SocketException)
        {
            // Another instance started in the same moment and won.
            listener.Dispose();
            return null;
        }
    }

    /// <summary>Calls <paramref name="show"/>, on a background thread, each time another launch asks for the widget.</summary>
    public void ListenForShowRequests(Action show) =>
        _ = Task.Run(async () =>
        {
            var buffer = new byte[16];
            while (true)
            {
                try
                {
                    using Socket client = await _listener.AcceptAsync();
                    int read = await client.ReceiveAsync(buffer);
                    if (Encoding.ASCII.GetString(buffer, 0, read) == ShowRequest)
                    {
                        show();
                    }
                }
                catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
                {
                    return;
                }
            }
        });

    public void Dispose()
    {
        _listener.Dispose();
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }
}
