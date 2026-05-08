namespace EchoDrop.Api;

public sealed record UpsertSinglePostBody(string Content, DateTimeOffset ScheduledAtUtc);
