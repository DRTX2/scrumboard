namespace ScrumBoard.Application.Models.Boards;

public sealed record BoardColumnItem(
    Guid Id,
    string Name,
    long Position,
    long Version,
    IReadOnlyList<BoardTask> Tasks,
    long TaskTotal,
    bool HasMoreTasks);
