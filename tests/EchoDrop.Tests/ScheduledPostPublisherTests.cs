using EchoDrop.Domain.Models;
using EchoDrop.Domain.Interfaces;
using EchoDrop.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EchoDrop.Tests;

public sealed class ScheduledPostPublisherTests
{
    [Fact]
    public async Task PublishDuePostsAsync_PublishesAndMarksEachDuePost()
    {
        var now = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);
        var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var duePosts = new List<ScheduledPost>
        {
            new(firstId, "First", now.AddMinutes(-10)),
            new(secondId, "Second", now.AddMinutes(-1))
        };

        var repository = new FakeRepository(duePosts);
        var provider = new FakeProvider();
        var publisher = new ScheduledPostPublisher(repository, provider, NullLogger<ScheduledPostPublisher>.Instance);

        var published = await publisher.PublishDuePostsAsync(now, CancellationToken.None);

        Assert.Equal(2, published);
        Assert.Equal(["First", "Second"], provider.PublishedContents);
        Assert.Equal([firstId, secondId], repository.MarkedPostIds);
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

    [Fact]
    public async Task PublishDuePostsAsync_ContinuesWhenOnePublishFails()
    {
        var now = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);
        var failedId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var passedId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var duePosts = new List<ScheduledPost>
        {
            new(failedId, "Fail", now.AddMinutes(-5)),
            new(passedId, "Pass", now.AddMinutes(-1))
        };

        var repository = new FakeRepository(duePosts);
        var provider = new FakeProvider(content => content == "Fail" ? throw new InvalidOperationException("boom") : "provider-id");
        var publisher = new ScheduledPostPublisher(repository, provider, NullLogger<ScheduledPostPublisher>.Instance);

        var published = await publisher.PublishDuePostsAsync(now, CancellationToken.None);

        Assert.Equal(1, published);
        Assert.Equal(["Fail", "Pass"], provider.PublishedContents);
        Assert.Equal([passedId], repository.MarkedPostIds);
    }

    private sealed class FakeRepository(IReadOnlyList<ScheduledPost> duePosts) : IScheduledPostRepository
    {
        private readonly IReadOnlyList<ScheduledPost> _duePosts = duePosts;

        public List<Guid> MarkedPostIds { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpsertAsync(ScheduledPost post, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpsertAsync(IReadOnlyList<ScheduledPost> posts, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ScheduledPost>> GetDuePostsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
            => Task.FromResult(_duePosts);

        public Task MarkAsPublishedAsync(Guid postId, string? providerPostId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken)
        {
            MarkedPostIds.Add(postId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProvider(Func<string, string?>? resultFactory = null) : IPostPublisher
    {
        private readonly Func<string, string?> _resultFactory = resultFactory ?? (_ => "provider-id");

        public List<string> PublishedContents { get; } = [];

        public Task<string?> PublishAsync(string content, CancellationToken cancellationToken)
        {
            PublishedContents.Add(content);
            return Task.FromResult(_resultFactory(content));
        }
    }
}
