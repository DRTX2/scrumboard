using Microsoft.AspNetCore.SignalR;
using ScrumBoard.Application.Abstractions;

namespace ScrumBoard.Api.Realtime;

internal sealed class SignalRBoardNotifier(IHubContext<BoardHub> hub) : IBoardNotifier
{
    public Task PublishAsync(Guid projectId, string eventName, object payload, CancellationToken cancellationToken) =>
        hub.Clients.Group(BoardHub.GroupName(projectId)).SendAsync(eventName, payload, cancellationToken);
}
