using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ScrumBoard.Application.Boards;

namespace ScrumBoard.Api.Realtime;

[Authorize]
internal sealed class BoardHub(BoardService boards, BoardPresence presence) : Hub
{
    private readonly HashSet<Guid> _subscriptions = [];

    public async Task SubscribeToBoard(Guid boardId)
    {
        await boards.GetMembersAsync(boardId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId), Context.ConnectionAborted);
        _subscriptions.Add(boardId);
        var users = presence.Join(boardId, Context.ConnectionId, CurrentUser());
        await Clients.Group(GroupName(boardId)).SendAsync("PresenceChanged", new { users, count = users.Count }, Context.ConnectionAborted);
    }

    public async Task UnsubscribeFromBoard(Guid boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(boardId));
        _subscriptions.Remove(boardId);
        var users = presence.Leave(boardId, Context.ConnectionId);
        await Clients.Group(GroupName(boardId)).SendAsync("PresenceChanged", new { users, count = users.Count });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var projectId in _subscriptions)
        {
            var users = presence.Leave(projectId, Context.ConnectionId);
            await Clients.Group(GroupName(projectId)).SendAsync("PresenceChanged", new { users, count = users.Count });
        }
        await base.OnDisconnectedAsync(exception);
    }

    internal static string GroupName(Guid projectId) => $"board:{projectId:N}";

    private PresenceUser CurrentUser()
    {
        var id = Guid.Parse(Context.User!.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        return new PresenceUser(id, Context.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? "User");
    }
}
