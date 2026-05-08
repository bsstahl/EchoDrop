using EchoDrop.Domain.Interfaces;

namespace EchoDrop.Services;

public sealed class ScheduledPostPublisher(
    IScheduledPostRepository scheduledPostRepository,
    IPostPublisher postPublisher,
    ILogger<ScheduledPostPublisher> logger)
{
    private readonly IScheduledPostRepository _scheduledPostRepository = scheduledPostRepository;
    private readonly IPostPublisher _postPublisher = postPublisher;
    private readonly ILogger<ScheduledPostPublisher> _logger = logger;

    public async Task<int> PublishDuePostsAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var duePosts = await _scheduledPostRepository.GetDuePostsAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        var publishedCount = 0;

        foreach (var post in duePosts)
        {
            try
            {
                var providerPostId = await _postPublisher.PublishAsync(post.Content, cancellationToken).ConfigureAwait(false);
                await _scheduledPostRepository.MarkAsPublishedAsync(post.Id, providerPostId, nowUtc, cancellationToken).ConfigureAwait(false);
                publishedCount++;

                _logger.LogInformation("Published scheduled post {PostId} with provider id {ProviderPostId}.", post.Id, providerPostId ?? "<none>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish scheduled post {PostId}.", post.Id);
            }
        }

        return publishedCount;
    }
}
