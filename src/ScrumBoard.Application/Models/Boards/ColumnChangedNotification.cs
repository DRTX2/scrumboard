namespace ScrumBoard.Application.Models.Boards;

public sealed record ColumnChangedNotification(Guid ProjectId, ColumnResult Column) : BoardNotification(ProjectId);
