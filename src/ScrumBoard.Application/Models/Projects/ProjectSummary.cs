using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Models.Projects;

public sealed record ProjectSummary(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status,
    ProjectRole Role,
    long Version,
    DateTimeOffset UpdatedAt);
