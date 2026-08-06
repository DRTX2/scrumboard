using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Models.Projects;

public sealed record ProjectDetails(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status,
    ProjectRole Role,
    long Version,
    long BoardVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
