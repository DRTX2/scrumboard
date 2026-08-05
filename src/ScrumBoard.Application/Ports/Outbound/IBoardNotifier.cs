using ScrumBoard.Application.Models.Boards;

namespace ScrumBoard.Application.Ports.Outbound;

public interface IBoardNotifier
{
    Task PublishAsync(BoardNotification notification, CancellationToken cancellationToken);
}
