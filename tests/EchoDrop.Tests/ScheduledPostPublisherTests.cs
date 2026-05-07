using EchoDrop.Models;
using EchoDrop.Providers;
using EchoDrop.Services;
using EchoDrop.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace EchoDrop.Tests;

public sealed class ScheduledPostPublisherTests
{
    [Fact]
    public async Task PublishDuePostsAsync_PublishesAndMarksEachDuePost()
    {
        var now = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);
        var duePosts = new List<ScheduledPost>
        {
            new(1, "First", now.AddMinutes(-10)),
            new(2, "Second", now.AddMinutes(-1))
        };

        var repository = new FakeRepository(duePosts);
        var provider = new FakeProvider();
        var publisher = new ScheduledPostPublisher(repository, provider, NullLogger<ScheduledPostPublisher>.Instance);

        var published = await publisher.PublishDuePostsAsync(now, CancellationToken.None);

        Assert.Equal(2, published);
        Assert.Equal(["First", "Second"], provider.PublishedContents);
        Assert.Equal([1L, 2L], repository.MarkedPostIds);
    }

    [Fact]
    public async Task PublishDuePostsAsync_DoesNothingWhenNoDuePosts()
    {
        var repository = new FakeRepository([]);
        var provider = new FakeProvider();
        var publisher = new ScheduledPostPublisher(repository, provider, NullLogger<ScheduledPostPublisher>.Instance);

        var published = await publisher.PublishDuePostsAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(0, published);
        Assert.Empty(provider.PublishedContents);
        Assert.Empty(repository.MarkedPostIds);
    }

    private sealed class FakeRepository(IReadOnlyList<ScheduledPost> duePosts) : IScheduledPostRepository
    {
        private readonly IReadOnlyList<ScheduledPost> _duePosts = duePosts;

        public List<long> MarkedPostIds { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ScheduledPost>> GetDuePostsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
            => Task.FromResult(_duePosts);

        public Task MarkAsPublishedAsync(long postId, string? providerPostId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken)
        {
            MarkedPostIds.Add(postId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProvider : IMastodonProvider
    {
        public List<string> PublishedContents { get; } = [];

        public Task<string?> PublishAsync(string content, CancellationToken cancellationToken)
        {
            PublishedContents.Add(content);
            return Task.FromResult<string?>("provider-id");
        }
    }
}
