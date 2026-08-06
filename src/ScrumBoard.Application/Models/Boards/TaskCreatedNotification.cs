namespace ScrumBoard.Application.Models.Boards;

public sealed record TaskCreatedNotification(Guid ProjectId, TaskResult Task) : BoardNotification(ProjectId);
