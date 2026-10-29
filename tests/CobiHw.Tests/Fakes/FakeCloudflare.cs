using System.Net;
using CobiHw.Core.Network;

namespace CobiHw.Tests.Fakes;

/// <summary>
/// Simulated Cloudflare speed test server: answers after 5 ms, reports 1.5 ms of server time
/// and moves data at a fixed line speed that concurrent transfers share, so results are predictable.
/// </summary>
/// <param name="bitsPerSecond">Simulated line speed in each direction.</param>
/// <param name="uploadSlowStart">How long the upload line crawls after its first use (see <see cref="SimulatedLine"/>).</param>
internal sealed class FakeCloudflare(double bitsPerSecond = 1e9, TimeSpan uploadSlowStart = default) : HttpMessageHandler
{
    /// <summary>Downloads of 100 MB or more are refused, like the real server does.</summary>
    public const long RefusedDownloadBytes = 100_000_000;

    /// <summary>Short phases (1 s, of which the first 0.25 s don't count) with 4 connections, so tests run fast.</summary>
    public static readonly SpeedTestTiming QuickTiming = new(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(250), 4);

    private readonly SimulatedLine _downLine = new(bitsPerSecond);
    private readonly SimulatedLine _upLine = new(bitsPerSecond, uploadSlowStart);
    private readonly Lock _lock = new();
    private long _largestDownload;
    private int _transfers;
    private int _mostTransfers;
    private int _cutUploads;

    /// <summary>Size of the largest download requested.</summary>
    public long LargestDownload
    {
        get
        {
            lock (_lock)
            {
                return _largestDownload;
            }
        }
    }

    /// <summary>Most downloads or uploads that ran at the same time.</summary>
    public int MostTransfers
    {
        get
        {
            lock (_lock)
            {
                return _mostTransfers;
            }
        }
    }

    /// <summary>Uploads whose body stopped with an error instead of ending: a dropped connection.</summary>
    public int CutUploads
    {
        get
        {
            lock (_lock)
            {
                return _cutUploads;
            }
        }
    }

    /// <summary>Speed test client against <paramref name="server"/>, with <see cref="QuickTiming"/>.</summary>
    /// <param name="server">Simulated server.</param>
    public static CloudflareSpeedTest Client(HttpMessageHandler server) => new(new HttpClient(server), QuickTiming);

    /// <summary>Size asked by a download URL (the "bytes" query value).</summary>
    /// <param name="request">Download request.</param>
    public static long RequestedBytes(HttpRequestMessage request) => long.Parse(request.RequestUri!.Query.Split('=')[1]);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken);
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Server-Timing", "cfRequestDuration;dur=1.5");

        if (request.Method == HttpMethod.Get)
        {
            long bytes = RequestedBytes(request);
            if (bytes >= RefusedDownloadBytes)
            {
                response.Dispose();
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            lock (_lock)
            {
                _largestDownload = Math.Max(_largestDownload, bytes);
            }

            TransferStarted();
            response.Content = new StreamContent(new ZeroStream(bytes, _downLine, TransferEnded));
            return response;
        }

        var counter = new CountingStream(_upLine);
        TransferStarted();
        try
        {
            await request.Content!.CopyToAsync(counter, cancellationToken);
        }
        catch
        {
            lock (_lock)
            {
                _cutUploads++;
            }

            throw;
        }
        finally
        {
            TransferEnded();
        }

        response.Content = new ByteArrayContent([]);
        return response;
    }

    private void TransferStarted()
    {
        lock (_lock)
        {
            _transfers++;
            _mostTransfers = Math.Max(_mostTransfers, _transfers);
        }
    }

    private void TransferEnded()
    {
        lock (_lock)
        {
            _transfers--;
        }
    }
}
