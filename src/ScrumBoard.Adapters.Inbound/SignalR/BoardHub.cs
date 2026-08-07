using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ScrumBoard.Application.Ports.Inbound.Boards;

namespace ScrumBoard.Adapters.Inbound.SignalR;

[Authorize]
public sealed class BoardHub(
    IBoardUseCase boards,
    BoardPresence presence,
    ILogger<BoardHub> logger) : Hub
{
    private static readonly Action<ILogger, Guid, Exception?> DisconnectPublishFailed =
        LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(1, "PresenceDisconnectPublishFailed"),
            "Could not publish disconnect presence update for project {ProjectId}");

    public async Task SubscribeToBoard(Guid projectId)
    {
        await boards.GetMembersAsync(projectId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, BoardGroups.For(projectId), Context.ConnectionAborted);
        var snapshot = presence.Join(projectId, Context.ConnectionId, CurrentUser());
        await PublishPresenceAsync(projectId, snapshot, Context.ConnectionAborted);
    }

    public async Task UnsubscribeFromBoard(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardGroups.For(projectId));
        var snapshot = presence.Leave(projectId, Context.ConnectionId);
        if (snapshot is not null) await PublishPresenceAsync(projectId, snapshot, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var update in presence.LeaveConnection(Context.ConnectionId))
        {
            try
            {
                await PublishPresenceAsync(update.ProjectId, update.Snapshot, CancellationToken.None);
            }
            catch (Exception publishException)
            {
                DisconnectPublishFailed(logger, update.ProjectId, publishException);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private Task PublishPresenceAsync(
        Guid projectId,
        PresenceSnapshot snapshot,
        CancellationToken cancellationToken) =>
        Clients.Group(BoardGroups.For(projectId))
            .SendAsync("PresenceChanged",
                new PresenceChangedPayload(snapshot.Users, snapshot.Users.Count, snapshot.Version), cancellationToken);

    private PresenceUser CurrentUser()
    {
        var id = Guid.Parse(Context.User!.FindFirst("sub")!.Value);
        return new PresenceUser(id, Context.User.FindFirst("name")?.Value ?? "User");
    }
}
