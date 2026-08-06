namespace ScrumBoard.Application.Models.Boards;

public sealed record TaskUpdatedNotification(Guid ProjectId, TaskResult Task) : BoardNotification(ProjectId);
