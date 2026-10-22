using System.Net;

namespace CobiHw.Tests.Fakes;

/// <summary>
/// Simulated Cloudflare speed test server: answers after 5 ms, reports 1.5 ms of server time
/// and moves data at a fixed rate, so results are predictable.
/// </summary>
/// <param name="bitsPerSecond">Simulated line speed in both directions.</param>
internal sealed class FakeCloudflare(double bitsPerSecond = 1e9) : HttpMessageHandler
{
    /// <summary>Downloads of 100 MB or more are refused, like the real server does.</summary>
    public const long RefusedDownloadBytes = 100_000_000;

    /// <summary>Bytes sent to the client in all downloads.</summary>
    public long DownloadedBytes { get; private set; }

    /// <summary>Bytes received in all uploads.</summary>
    public long UploadedBytes { get; private set; }

    /// <summary>Size of the largest download requested.</summary>
    public long LargestDownload { get; private set; }

    /// <summary>How many times the largest size was requested.</summary>
    public int LargestDownloadCount { get; private set; }

    /// <summary>False if any upload sent a different number of bytes than its Content-Length.</summary>
    public bool UploadLengthsMatched { get; private set; } = true;

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

            DownloadedBytes += bytes;
            if (bytes > LargestDownload)
            {
                LargestDownload = bytes;
                LargestDownloadCount = 0;
            }

            if (bytes == LargestDownload)
            {
                LargestDownloadCount++;
            }

            response.Content = new StreamContent(new ZeroStream(bytes, bitsPerSecond));
        }
        else
        {
            long declared = request.Content!.Headers.ContentLength ?? -1;
            var counter = new CountingStream(bitsPerSecond);
            await request.Content.CopyToAsync(counter, cancellationToken);

            if (counter.Length != declared)
            {
                UploadLengthsMatched = false;
            }

            UploadedBytes += counter.Length;
            response.Content = new ByteArrayContent([]);
        }

        return response;
    }
}
