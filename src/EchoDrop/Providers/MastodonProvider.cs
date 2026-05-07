using System.Net.Http.Headers;
using System.Text.Json;
using EchoDrop.Configuration;
using Microsoft.Extensions.Options;

namespace EchoDrop.Providers;

public sealed class MastodonProvider(HttpClient httpClient, IOptions<MastodonOptions> options) : IMastodonProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly MastodonOptions _options = options.Value;

    public async Task<string?> PublishAsync(string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new InvalidOperationException("Mastodon AccessToken is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/statuses")
        {
            Content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("status", content)
            ])
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return payload.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }
}
