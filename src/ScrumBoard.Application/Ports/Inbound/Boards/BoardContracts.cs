using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Ports.Inbound.Boards;

public sealed record CreateColumn(string Name);
public sealed record UpdateColumn(string Name);
public sealed record MoveColumn(Guid? BeforeColumnId, Guid? AfterColumnId);

public sealed record CreateTask(
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? AssigneeId,
    DateOnly? DueDate);
public sealed record UpdateTask(string Title, string? Description, TaskPriority Priority, Guid? AssigneeId, DateOnly? DueDate);
public sealed record MoveTask(Guid ColumnId, Guid? BeforeTaskId, Guid? AfterTaskId);
