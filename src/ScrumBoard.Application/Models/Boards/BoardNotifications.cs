namespace ScrumBoard.Application.Models.Boards;

public abstract record BoardNotification(Guid ProjectId);

public sealed record ColumnChangedNotification(Guid ProjectId, ColumnResult Column) : BoardNotification(ProjectId);
public sealed record ColumnDeletedNotification(Guid ProjectId, Guid ColumnId, long BoardVersion) : BoardNotification(ProjectId);
public sealed record TaskCreatedNotification(Guid ProjectId, TaskResult Task) : BoardNotification(ProjectId);
public sealed record TaskUpdatedNotification(Guid ProjectId, TaskResult Task) : BoardNotification(ProjectId);
public sealed record TaskMovedNotification(Guid ProjectId, TaskResult Task) : BoardNotification(ProjectId);
public sealed record TaskDeletedNotification(Guid ProjectId, Guid TaskId, long BoardVersion) : BoardNotification(ProjectId);
