using System.Net;

namespace CobiHw.Tests.Fakes;

/// <summary>Simulated server that answers 403 Forbidden from a given transfer size on.</summary>
/// <param name="download">Refuses downloads when true, uploads when false.</param>
/// <param name="fromBytes">Smallest transfer that is refused.</param>
internal sealed class RefusingCloudflare(bool download, long fromBytes) : HttpMessageHandler
{
    private readonly HttpMessageInvoker _server = new(new FakeCloudflare());

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool isDownload = request.Method == HttpMethod.Get;
        long bytes = isDownload ? FakeCloudflare.RequestedBytes(request) : request.Content!.Headers.ContentLength ?? 0;

        if (isDownload == download && bytes >= fromBytes)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        }

        return _server.SendAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _server.Dispose();
        }

        base.Dispose(disposing);
    }
}
