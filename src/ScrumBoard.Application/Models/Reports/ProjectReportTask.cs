using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Models.Reports;

public sealed record ProjectReportTask(
    string Title,
    string Column,
    string? Assignee,
    TaskPriority Priority,
    DateTimeOffset CreatedAt,
    DateOnly? DueDate);
