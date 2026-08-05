using Microsoft.EntityFrameworkCore;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;

internal sealed class ReportDataSource(ScrumBoardDbContext dbContext) : IReportDataSource
{
    public async Task<ProjectReportData?> GetAsync(
        Guid projectId,
        TaskFilter filter,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Description,
                item.StartDate,
                item.ExpectedEndDate,
                item.Status
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        var query =
            from task in dbContext.Tasks.AsNoTracking()
            join column in dbContext.Columns.AsNoTracking() on task.ColumnId equals column.Id
            join userValue in dbContext.Users.AsNoTracking() on task.AssigneeId equals userValue.Id into taskUsers
            from assignee in taskUsers.DefaultIfEmpty()
            where task.ProjectId == projectId
            select new
            {
                task.Title,
                task.Description,
                task.AssigneeId,
                task.Priority,
                task.CreatedAt,
                ColumnName = column.Name,
                AssigneeName = assignee == null ? null : assignee.Name
            };

        if (filter.AssigneeId is not null) query = query.Where(row => row.AssigneeId == filter.AssigneeId);
        if (filter.Priority is not null) query = query.Where(row => row.Priority == filter.Priority);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = PostgreSqlLike.ContainsLiteral(filter.Search);
            query = query.Where(row => EF.Functions.ILike(
                    row.Title, pattern, PostgreSqlLike.EscapeCharacter) ||
                (row.Description != null && EF.Functions.ILike(
                    row.Description, pattern, PostgreSqlLike.EscapeCharacter)));
        }

        var tasks = await query.OrderBy(row => row.ColumnName).ThenBy(row => row.CreatedAt)
            .Select(row => new ProjectReportTask(
                row.Title,
                row.ColumnName,
                row.AssigneeName,
                row.Priority,
                row.CreatedAt))
            .ToListAsync(cancellationToken);
        return new ProjectReportData(
            project.Id,
            project.Name,
            project.Description,
            project.StartDate,
            project.ExpectedEndDate,
            project.Status,
            generatedAt,
            tasks);
    }
}
