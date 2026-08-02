using Microsoft.EntityFrameworkCore;
using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Boards;
using ScrumBoard.Application.Reports;

namespace ScrumBoard.Infrastructure.Persistence;

internal sealed class ReportDataSource(ScrumBoardDbContext dbContext) : IReportDataSource
{
    public async Task<ProjectReportData?> GetAsync(
        Guid projectId,
        Guid userId,
        BoardFilter filter,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        var query =
            from project in dbContext.Projects.AsNoTracking()
            join membership in dbContext.ProjectMembers.AsNoTracking() on project.Id equals membership.ProjectId
            where project.Id == projectId && membership.UserId == userId
            join taskValue in dbContext.Tasks.AsNoTracking() on project.Id equals taskValue.ProjectId into projectTasks
            from task in projectTasks.DefaultIfEmpty()
            join columnValue in dbContext.Columns.AsNoTracking() on task.ColumnId equals columnValue.Id into taskColumns
            from column in taskColumns.DefaultIfEmpty()
            join userValue in dbContext.Users.AsNoTracking() on task.AssigneeId equals userValue.Id into taskUsers
            from assignee in taskUsers.DefaultIfEmpty()
            select new
            {
                Project = project,
                TaskId = task == null ? (Guid?)null : task.Id,
                TaskTitle = task == null ? null : task.Title,
                ColumnName = column == null ? null : column.Name,
                AssigneeName = assignee == null ? null : assignee.Name,
                Priority = task == null ? (Domain.Tasks.TaskPriority?)null : task.Priority,
                TaskCreatedAt = task == null ? (DateTimeOffset?)null : task.CreatedAt,
                TaskAssigneeId = task == null ? null : task.AssigneeId,
                TaskDescription = task == null ? null : task.Description
            };

        if (filter.AssigneeId is not null) query = query.Where(row => row.TaskId == null || row.TaskAssigneeId == filter.AssigneeId);
        if (filter.Priority is not null) query = query.Where(row => row.TaskId == null || row.Priority == filter.Priority);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(row => row.TaskId == null ||
                (row.TaskTitle != null && EF.Functions.ILike(row.TaskTitle, $"%{filter.Search}%")) ||
                (row.TaskDescription != null && EF.Functions.ILike(row.TaskDescription, $"%{filter.Search}%")));
        }

        var rows = await query.OrderBy(row => row.ColumnName).ThenBy(row => row.TaskCreatedAt).ToListAsync(cancellationToken);
        var first = rows.FirstOrDefault();
        if (first is null) return null;
        return new ProjectReportData(
            first.Project.Id,
            first.Project.Name,
            first.Project.Description,
            first.Project.StartDate,
            first.Project.ExpectedEndDate,
            first.Project.Status,
            generatedAt,
            rows.Where(row => row.TaskId is not null).Select(row => new ProjectReportTask(
                row.TaskTitle!, row.ColumnName!, row.AssigneeName, row.Priority!.Value, row.TaskCreatedAt!.Value)).ToList());
    }
}
