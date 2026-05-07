using EchoDrop.Publishing;
using EchoDrop.Publisher.Mastodon;

namespace EchoDrop.Providers;

public sealed class MastodonPostPublisherAdapter(IMastodonPostPublisher mastodonPostPublisher) : IPostPublisher
{
    private readonly IMastodonPostPublisher _mastodonPostPublisher = mastodonPostPublisher;

    public Task<string?> PublishAsync(string content, CancellationToken cancellationToken)
        => _mastodonPostPublisher.PublishAsync(content, cancellationToken);
}
