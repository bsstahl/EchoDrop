using EchoDrop.Providers;
using EchoDrop.Storage;

namespace EchoDrop.Services;

public sealed class ScheduledPostPublisher(
    IScheduledPostRepository scheduledPostRepository,
    IMastodonProvider mastodonProvider,
    ILogger<ScheduledPostPublisher> logger)
{
    private readonly IScheduledPostRepository _scheduledPostRepository = scheduledPostRepository;
    private readonly IMastodonProvider _mastodonProvider = mastodonProvider;
    private readonly ILogger<ScheduledPostPublisher> _logger = logger;

    public async Task<int> PublishDuePostsAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var duePosts = await _scheduledPostRepository.GetDuePostsAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        var publishedCount = 0;

        foreach (var post in duePosts)
        {
            var providerPostId = await _mastodonProvider.PublishAsync(post.Content, cancellationToken).ConfigureAwait(false);
            await _scheduledPostRepository.MarkAsPublishedAsync(post.Id, providerPostId, nowUtc, cancellationToken).ConfigureAwait(false);
            publishedCount++;

            _logger.LogInformation("Published scheduled post {PostId} as Mastodon status {ProviderPostId}.", post.Id, providerPostId ?? "<none>");
        }

        return publishedCount;
    }
}
