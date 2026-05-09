using EchoDrop.Domain.Interfaces;
using EchoDrop.Domain.Models;
using EchoDrop.Configuration;
using Microsoft.Extensions.Options;

namespace EchoDrop.Api;

public static class DataUpdateApi
{
    public static IEndpointRouteBuilder MapDataUpdateApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPut("/api/posts/{id:guid}", UpsertSinglePostAsync);
        endpoints.MapPut("/api/posts", UpsertPostsAsync);
        endpoints.MapDelete("/api/posts/{id:guid}", CancelPostAsync);

        return endpoints;
    }

    private static async Task<IResult> UpsertSinglePostAsync(
        Guid id,
        UpsertSinglePostBody request,
        IScheduledPostRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest("Content is required.");
        }

        var post = new ScheduledPost(id, request.Content, request.ScheduledAtUtc);
        await repository.UpsertAsync(post, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> UpsertPostsAsync(
        IReadOnlyList<UpsertScheduledPostRequest> requests,
        IScheduledPostRepository repository,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return Results.BadRequest("At least one post is required.");
        }

        if (requests.Any(request => string.IsNullOrWhiteSpace(request.Content)))
        {
            return Results.BadRequest("Content is required.");
        }

        if (requests.Select(request => request.Id).Distinct().Count() != requests.Count)
        {
            return Results.BadRequest("Duplicate post ids are not allowed.");
        }

        var posts = requests
            .Select(request => new ScheduledPost(request.Id, request.Content, request.ScheduledAtUtc))
            .ToArray();

        await repository.UpsertAsync(posts, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> CancelPostAsync(
        Guid id,
        IScheduledPostRepository repository,
        IOptions<WorkerOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var cancelLeadTime = TimeSpan.FromSeconds(Math.Max(0, options.Value.CancelLeadTimeSeconds));
        var latestCancelableAtUtc = timeProvider.GetUtcNow().Add(cancelLeadTime);
        var result = await repository.CancelScheduledPostAsync(id, latestCancelableAtUtc, cancellationToken).ConfigureAwait(false);

        return result switch
        {
            CancelScheduledPostResult.Canceled => Results.NoContent(),
            CancelScheduledPostResult.NotFound => Results.NotFound(),
            CancelScheduledPostResult.AlreadyPublished => Results.Conflict("Post has already been published."),
            CancelScheduledPostResult.TooCloseToPublish => Results.Conflict("Post is too close to scheduled publication time to cancel."),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

}
