using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Adapters.Inbound.Http.Contracts;

public sealed record ProjectDetailsResponse(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status,
    ProjectRole Role,
    string Etag,
    string BoardEtag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
