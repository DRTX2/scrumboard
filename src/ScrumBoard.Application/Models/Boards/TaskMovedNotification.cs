namespace ScrumBoard.Application.Models.Boards;

public sealed record TaskMovedNotification(Guid ProjectId, TaskResult Task) : BoardNotification(ProjectId);
