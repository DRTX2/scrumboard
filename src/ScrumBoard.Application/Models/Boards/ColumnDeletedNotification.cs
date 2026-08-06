namespace ScrumBoard.Application.Models.Boards;

public sealed record ColumnDeletedNotification(Guid ProjectId, Guid ColumnId, long BoardVersion)
    : BoardNotification(ProjectId);
