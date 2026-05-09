using EchoDrop.Domain.Interfaces;
using EchoDrop.Domain.Models;

namespace EchoDrop.Api;

public static class DataUpdateApi
{
    public static IEndpointRouteBuilder MapDataUpdateApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPut("/api/posts/{id:guid}", UpsertSinglePostAsync);
        endpoints.MapPut("/api/posts", UpsertPostsAsync);

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

}
