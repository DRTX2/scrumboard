using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumBoard.Api.Adapters.SignalR;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.UnitTests.Adapters.SignalR;

public sealed class SignalRBoardNotifierTests
{
    [Fact]
    public async Task Publish_MapsTypedNotificationsToTheExistingWireProtocol()
    {
        var projectId = Guid.NewGuid();
        var column = new ColumnResult(Guid.NewGuid(), projectId, "Backlog", 1024, 1, 2);
        var task = new TaskResult(Guid.NewGuid(), projectId, column.Id, "Task", null, TaskPriority.High,
            null, null, 1024, 1, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        BoardNotification[] notifications =
        [
            new ColumnChangedNotification(projectId, column),
            new ColumnDeletedNotification(projectId, column.Id, 3),
            new TaskCreatedNotification(projectId, task),
            new TaskUpdatedNotification(projectId, task),
            new TaskMovedNotification(projectId, task),
            new TaskDeletedNotification(projectId, task.Id, 4)
        ];
        var expectedMethods = new[]
        {
            "ColumnChanged", "ColumnChanged", "TaskCreated", "TaskUpdated", "TaskMoved", "TaskDeleted"
        };

        for (var index = 0; index < notifications.Length; index++)
        {
            var proxy = new RecordingClientProxy();
            var clients = new RecordingHubClients(proxy);
            var notifier = CreateNotifier(clients, new PostCommitActionQueue());

            await notifier.PublishAsync(notifications[index], default);

            Assert.Equal($"board:{projectId:N}", clients.GroupName);
            Assert.Equal(expectedMethods[index], proxy.Method);
        }
    }

    [Fact]
    public async Task Publish_MapsDeletionPayloadsInsideTheSignalRAdapter()
    {
        var projectId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var proxy = new RecordingClientProxy();
        var notifier = CreateNotifier(new RecordingHubClients(proxy), new PostCommitActionQueue());

        await notifier.PublishAsync(new ColumnDeletedNotification(projectId, columnId, 7), default);

        var payload = Assert.IsType<ColumnDeletedPayload>(Assert.Single(proxy.Arguments!));
        Assert.Equal(columnId, payload.ColumnId);
        Assert.True(payload.Deleted);
        Assert.Equal(7, payload.BoardVersion);
    }

    [Fact]
    public async Task Publish_WhenDeferred_SendsOnlyAfterCommitQueueIsDrained()
    {
        var queue = new PostCommitActionQueue();
        queue.BeginDeferral();
        var proxy = new RecordingClientProxy();
        var notifier = CreateNotifier(new RecordingHubClients(proxy), queue);
        var notification = new TaskDeletedNotification(Guid.NewGuid(), Guid.NewGuid(), 2);

        await notifier.PublishAsync(notification, default);
        Assert.Null(proxy.Method);

        await queue.DrainAsync(default);
        Assert.Equal("TaskDeleted", proxy.Method);
    }

    private static SignalRBoardNotifier CreateNotifier(IHubClients clients, PostCommitActionQueue queue) =>
        new(new StubHubContext(clients), queue, NullLogger<SignalRBoardNotifier>.Instance);

    private sealed class StubHubContext(IHubClients clients) : IHubContext<BoardHub>
    {
        public IHubClients Clients { get; } = clients;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class RecordingHubClients(RecordingClientProxy proxy) : IHubClients
    {
        public string? GroupName { get; private set; }
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Client(string connectionId) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;
        public IClientProxy Group(string groupName)
        {
            GroupName = groupName;
            return proxy;
        }
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;
        public IClientProxy User(string userId) => proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            Method = method;
            Arguments = args;
            return Task.CompletedTask;
        }
    }
}
