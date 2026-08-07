using ScrumBoard.Application.Models.Boards;

namespace ScrumBoard.Application.Ports.Out;

public interface IBoardNotifier
{
    Task PublishAsync(BoardNotification notification, CancellationToken cancellationToken);
}
