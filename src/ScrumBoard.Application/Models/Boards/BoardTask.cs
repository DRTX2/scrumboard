using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Models.Boards;

public sealed record BoardTask(
    Guid Id,
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid AssigneeId,
    string AssigneeName,
    DateOnly? DueDate,
    long Position,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
