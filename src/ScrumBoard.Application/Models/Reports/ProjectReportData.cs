using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Models.Reports;

public sealed record ProjectReportData(
    Guid ProjectId,
    string ProjectName,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ProjectReportTask> Tasks);
