using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Ports.Inbound.Boards;

public sealed record CreateTask(
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid AssigneeId,
    DateOnly? DueDate);
