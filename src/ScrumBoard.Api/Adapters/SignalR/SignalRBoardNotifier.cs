using Microsoft.AspNetCore.SignalR;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Api.Adapters.SignalR;

internal sealed class SignalRBoardNotifier(
    IHubContext<BoardHub> hub,
    PostCommitActionQueue postCommitActions,
    ILogger<SignalRBoardNotifier> logger) : IBoardNotifier
{
    private static readonly Action<ILogger, string, Exception?> PublishFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "RealtimePublishFailed"),
            "Could not publish board notification {NotificationType}");

    public Task PublishAsync(BoardNotification notification, CancellationToken cancellationToken)
    {
        if (postCommitActions.TryEnqueue(token => PublishBestEffortAsync(notification, token))) return Task.CompletedTask;
        return PublishBestEffortAsync(notification, cancellationToken);
    }

    private async Task PublishBestEffortAsync(BoardNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var client = hub.Clients.Group(BoardGroups.For(notification.ProjectId));
            await (notification switch
            {
                ColumnChangedNotification value => client.SendAsync("ColumnChanged", value.Column, cancellationToken),
                ColumnDeletedNotification value => client.SendAsync("ColumnChanged",
                    new ColumnDeletedPayload(value.ColumnId, true, value.BoardVersion), cancellationToken),
                TaskCreatedNotification value => client.SendAsync("TaskCreated", value.Task, cancellationToken),
                TaskUpdatedNotification value => client.SendAsync("TaskUpdated", value.Task, cancellationToken),
                TaskMovedNotification value => client.SendAsync("TaskMoved", value.Task, cancellationToken),
                TaskDeletedNotification value => client.SendAsync("TaskDeleted",
                    new TaskDeletedPayload(value.TaskId, value.BoardVersion), cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(notification), notification, "Unknown board notification.")
            });
        }
        catch (Exception exception)
        {
            PublishFailed(logger, notification.GetType().Name, exception);
        }
    }
}
