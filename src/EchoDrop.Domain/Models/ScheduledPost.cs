namespace EchoDrop.Domain.Models;

public sealed record ScheduledPost(Guid Id, string Content, DateTimeOffset ScheduledAtUtc);
