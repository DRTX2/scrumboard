namespace ScrumBoard.Application.Models.Projects;

public sealed record ProjectSearchCriteria(
    int Page,
    int PageSize,
    string? Search,
    ProjectSortField Sort,
    SortDirection Direction);
