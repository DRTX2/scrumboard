using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record TaskMutationResponse(
    Guid Id,
    Guid ProjectId,
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid AssigneeId,
    DateOnly? DueDate,
    long Position,
    string Etag,
    string BoardEtag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
