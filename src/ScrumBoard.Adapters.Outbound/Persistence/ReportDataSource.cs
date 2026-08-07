using Microsoft.EntityFrameworkCore;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Out;

namespace ScrumBoard.Adapters.Outbound.Persistence;

internal sealed class ReportDataSource(ScrumBoardDbContext dbContext) : IReportDataSource
{
    public async Task<ProjectReportData?> GetAsync(
        Guid projectId,
        Guid userId,
        TaskFilter filter,
        DateTimeOffset generatedAt,
        int taskRowLimit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(taskRowLimit, 1);

        var tasks = dbContext.Tasks.AsNoTracking().Where(task => task.ProjectId == projectId);
        if (filter.AssigneeId is not null) tasks = tasks.Where(task => task.AssigneeId == filter.AssigneeId);
        if (filter.Priority is not null) tasks = tasks.Where(task => task.Priority == filter.Priority);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = PostgreSqlLike.ContainsLiteral(filter.Search);
            tasks = tasks.Where(task => EF.Functions.ILike(
                    task.Title, pattern, PostgreSqlLike.EscapeCharacter) ||
                (task.Description != null && EF.Functions.ILike(
                    task.Description, pattern, PostgreSqlLike.EscapeCharacter)));
        }

        var rows = await (
                from project in dbContext.Projects.AsNoTracking()
                join membership in dbContext.ProjectMembers.AsNoTracking()
                    on project.Id equals membership.ProjectId
                where project.Id == projectId && membership.UserId == userId
                join taskValue in tasks on project.Id equals taskValue.ProjectId into projectTasks
                from task in projectTasks.DefaultIfEmpty()
                join columnValue in dbContext.Columns.AsNoTracking()
                    on task.ColumnId equals columnValue.Id into taskColumns
                from column in taskColumns.DefaultIfEmpty()
                join assigneeValue in dbContext.Users.AsNoTracking()
                    on task.AssigneeId equals assigneeValue.Id into taskAssignees
                from assignee in taskAssignees.DefaultIfEmpty()
                orderby column.Position, column.Id, task.Position, task.Id
                select new
                {
                    project.Id,
                    project.Name,
                    project.Description,
                    project.StartDate,
                    project.ExpectedEndDate,
                    project.Status,
                    TaskId = (Guid?)task.Id,
                    TaskTitle = task.Title,
                    ColumnName = column.Name,
                    AssigneeName = assignee == null ? null : assignee.Name,
                    TaskPriority = (ScrumBoard.Domain.Tasks.TaskPriority?)task.Priority,
                    TaskCreatedAt = (DateTimeOffset?)task.CreatedAt,
                    task.DueDate
                })
            .Take(taskRowLimit)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return null;

        var projectData = rows[0];
        return new ProjectReportData(
            projectData.Id,
            projectData.Name,
            projectData.Description,
            projectData.StartDate,
            projectData.ExpectedEndDate,
            projectData.Status,
            generatedAt,
            rows.Where(row => row.TaskId is not null)
                .Select(row => new ProjectReportTask(
                    row.TaskTitle,
                    row.ColumnName,
                    row.AssigneeName,
                    row.TaskPriority!.Value,
                    row.TaskCreatedAt!.Value,
                    row.DueDate))
                .ToList());
    }
}
