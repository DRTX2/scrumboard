using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Models.Projects;

public enum ProjectSortField
{
    UpdatedAt,
    Name,
    StartDate,
    Status
}

public enum SortDirection
{
    Ascending,
    Descending
}

public sealed record ProjectSearchCriteria(
    int Page,
    int PageSize,
    string? Search,
    ProjectSortField Sort,
    SortDirection Direction);

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
