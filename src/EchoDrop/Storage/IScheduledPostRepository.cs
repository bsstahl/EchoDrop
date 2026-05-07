using EchoDrop.Models;

namespace EchoDrop.Storage;

public interface IScheduledPostRepository
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledPost>> GetDuePostsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken);

    Task MarkAsPublishedAsync(long postId, string? providerPostId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken);
}
