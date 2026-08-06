using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record BoardTaskResponse(
    Guid Id,
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid AssigneeId,
    AssigneeResponse Assignee,
    DateOnly? DueDate,
    long Position,
    string Etag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
