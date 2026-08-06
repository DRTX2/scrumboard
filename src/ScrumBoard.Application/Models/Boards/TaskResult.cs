using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Models.Boards;

public sealed record TaskResult(
    Guid Id,
    Guid ProjectId,
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid AssigneeId,
    DateOnly? DueDate,
    long Position,
    long Version,
    long BoardVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
