namespace ScrumBoard.Application.Abstractions;

public interface IBoardNotifier
{
    Task PublishAsync(Guid projectId, string eventName, object payload, CancellationToken cancellationToken);
}
