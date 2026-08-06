namespace ScrumBoard.Application.Models.Boards;

public sealed record TaskDeletedNotification(Guid ProjectId, Guid TaskId, long BoardVersion)
    : BoardNotification(ProjectId);
