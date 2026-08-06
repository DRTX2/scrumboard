using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status,
    ProjectRole Role,
    string Etag,
    DateTimeOffset UpdatedAt);
