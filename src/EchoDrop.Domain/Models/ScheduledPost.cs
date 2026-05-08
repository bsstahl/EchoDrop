namespace EchoDrop.Domain.Models;

public sealed record ScheduledPost(long Id, string Content, DateTimeOffset ScheduledAtUtc);
