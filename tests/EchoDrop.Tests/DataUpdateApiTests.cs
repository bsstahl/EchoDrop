using System.Net;
using System.Net.Http.Json;
using System.Diagnostics.CodeAnalysis;
using EchoDrop.Domain.Interfaces;
using EchoDrop.Domain.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EchoDrop.Tests;

[ExcludeFromCodeCoverage]
public sealed class DataUpdateApiTests
{
    [Fact]
    public async Task PutSinglePost_ValidRequest_UpsertsPost()
    {
        var repository = new RecordingRepository();
        using var factory = CreateFactory(repository);
        using var client = factory.CreateClient();
        var postId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var scheduledAtUtc = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);

        var response = await client.PutAsJsonAsync($"/api/posts/{postId:D}", new
        {
            Content = "Hello world",
            ScheduledAtUtc = scheduledAtUtc
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(repository.SingleUpsert);
        Assert.Equal(postId, repository.SingleUpsert.Id);
        Assert.Equal("Hello world", repository.SingleUpsert.Content);
        Assert.Equal(scheduledAtUtc, repository.SingleUpsert.ScheduledAtUtc);
        Assert.Empty(repository.BulkUpserts);
    }

    [Fact]
    public async Task PutSinglePost_BlankContent_ReturnsBadRequest()
    {
        var repository = new RecordingRepository();
        using var factory = CreateFactory(repository);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/posts/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", new
        {
            Content = "   ",
            ScheduledAtUtc = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(repository.SingleUpsert);
        Assert.Empty(repository.BulkUpserts);
    }

    [Fact]
    public async Task PutPosts_ValidRequest_UpsertsPosts()
    {
        var repository = new RecordingRepository();
        using var factory = CreateFactory(repository);
        using var client = factory.CreateClient();
        var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var response = await client.PutAsJsonAsync("/api/posts", new[]
        {
            new
            {
                Id = firstId,
                Content = "First",
                ScheduledAtUtc = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero)
            },
            new
            {
                Id = secondId,
                Content = "Second",
                ScheduledAtUtc = new DateTimeOffset(2026, 01, 02, 12, 00, 00, TimeSpan.Zero)
            }
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(repository.SingleUpsert);
        Assert.Collection(
            repository.BulkUpserts,
            post =>
            {
                Assert.Equal(firstId, post.Id);
                Assert.Equal("First", post.Content);
            },
            post =>
            {
                Assert.Equal(secondId, post.Id);
                Assert.Equal("Second", post.Content);
            });
    }

    [Fact]
    public async Task PutPosts_EmptyRequest_ReturnsBadRequest()
    {
        var repository = new RecordingRepository();
        using var factory = CreateFactory(repository);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/posts", Array.Empty<object>());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(repository.SingleUpsert);
        Assert.Empty(repository.BulkUpserts);
    }

    [Fact]
    public async Task PutPosts_DuplicateIds_ReturnsBadRequest()
    {
        var repository = new RecordingRepository();
        using var factory = CreateFactory(repository);
        using var client = factory.CreateClient();
        var postId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var response = await client.PutAsJsonAsync("/api/posts", new[]
        {
            new
            {
                Id = postId,
                Content = "First",
                ScheduledAtUtc = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero)
            },
            new
            {
                Id = postId,
                Content = "Second",
                ScheduledAtUtc = new DateTimeOffset(2026, 01, 02, 12, 00, 00, TimeSpan.Zero)
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(repository.SingleUpsert);
        Assert.Empty(repository.BulkUpserts);
    }

    [Fact]
    public async Task PutPosts_BlankContent_ReturnsBadRequest()
    {
        var repository = new RecordingRepository();
        using var factory = CreateFactory(repository);
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/posts", new[]
        {
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Content = "   ",
                ScheduledAtUtc = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero)
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(repository.SingleUpsert);
        Assert.Empty(repository.BulkUpserts);
    }

    private static WebApplicationFactory<Program> CreateFactory(RecordingRepository repository)
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IHostedService>();
                    services.RemoveAll<IScheduledPostRepository>();
                    services.AddSingleton<IScheduledPostRepository>(repository);
                });
            });

    private sealed class RecordingRepository : IScheduledPostRepository
    {
        public ScheduledPost? SingleUpsert { get; private set; }

        public IReadOnlyList<ScheduledPost> BulkUpserts { get; private set; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UpsertAsync(ScheduledPost post, CancellationToken cancellationToken)
        {
            SingleUpsert = post;
            return Task.CompletedTask;
        }

        public Task UpsertAsync(IReadOnlyList<ScheduledPost> posts, CancellationToken cancellationToken)
        {
            BulkUpserts = posts.ToArray();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScheduledPost>> GetDuePostsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ScheduledPost>>([]);

        public Task MarkAsPublishedAsync(Guid postId, string? providerPostId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
