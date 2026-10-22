namespace CobiHw.Tests.Fakes;

/// <summary>Simulates a network with no connection: every request fails.</summary>
internal sealed class FailingHandler : HttpMessageHandler
{
    /// <summary>Message of the simulated network error.</summary>
    public const string Message = "No connection could be made.";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException(Message);
}
