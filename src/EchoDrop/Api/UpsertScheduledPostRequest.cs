namespace EchoDrop.Api;

public sealed record UpsertScheduledPostRequest(Guid Id, string Content, DateTimeOffset ScheduledAtUtc);
