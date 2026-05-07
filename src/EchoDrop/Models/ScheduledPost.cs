namespace EchoDrop.Models;

public sealed record ScheduledPost(long Id, string Content, DateTimeOffset ScheduledAtUtc);
