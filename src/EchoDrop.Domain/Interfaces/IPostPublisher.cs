namespace EchoDrop.Domain.Interfaces;

public interface IPostPublisher
{
    Task<string?> PublishAsync(string content, CancellationToken cancellationToken);
}
