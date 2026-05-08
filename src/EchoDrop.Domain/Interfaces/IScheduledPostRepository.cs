using EchoDrop.Domain.Models;

namespace EchoDrop.Domain.Interfaces;

public interface IScheduledPostRepository
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken);

    Task UpsertAsync(ScheduledPost post, CancellationToken cancellationToken);

    Task UpsertAsync(IReadOnlyList<ScheduledPost> posts, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledPost>> GetDuePostsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken);

    Task MarkAsPublishedAsync(Guid postId, string? providerPostId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken);
}
