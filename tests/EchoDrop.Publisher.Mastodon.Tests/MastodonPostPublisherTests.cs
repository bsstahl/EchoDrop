using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EchoDrop.Publisher.Mastodon;
using EchoDrop.Publisher.Mastodon.Configuration;
using Microsoft.Extensions.Options;

namespace EchoDrop.Publisher.Mastodon.Tests;

public sealed class MastodonPostPublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsExpectedRequestAndReturnsId()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            capturedBody = request.Content is null ? null : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{" + "\"id\":\"12345\"}", Encoding.UTF8, "application/json")
            });
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.social/")
        };

        var sut = new MastodonPostPublisher(httpClient, Options.Create(new MastodonOptions { AccessToken = "token" }));

        var result = await sut.PublishAsync("Hello world", CancellationToken.None);

        Assert.Equal("12345", result);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(new Uri("https://example.social/api/v1/statuses"), capturedRequest.RequestUri);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "token"), capturedRequest.Headers.Authorization);
        Assert.Equal("status=Hello+world", capturedBody);
    }

    [Fact]
    public async Task PublishAsync_ThrowsWhenAccessTokenMissing()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("Should not call HTTP"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.social/")
        };

        var sut = new MastodonPostPublisher(httpClient, Options.Create(new MastodonOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.PublishAsync("Hello world", CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_ThrowsWhenMastodonReturnsError()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.social/")
        };

        var sut = new MastodonPostPublisher(httpClient, Options.Create(new MastodonOptions { AccessToken = "token" }));

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.PublishAsync("Hello world", CancellationToken.None));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _responder(request, cancellationToken);
    }
}
