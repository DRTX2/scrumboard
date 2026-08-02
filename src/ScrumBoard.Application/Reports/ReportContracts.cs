using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Reports;

public sealed record ProjectReportTask(
    string Title,
    string Column,
    string? Assignee,
    TaskPriority Priority,
    DateTimeOffset CreatedAt);

public sealed record ProjectReportData(
    Guid ProjectId,
    string ProjectName,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ProjectReportTask> Tasks);

public sealed record GeneratedReport(byte[] Content, string ContentType, string FileName);
