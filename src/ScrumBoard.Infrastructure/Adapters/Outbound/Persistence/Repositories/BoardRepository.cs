using Microsoft.EntityFrameworkCore;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Repositories;

internal sealed class BoardRepository(ScrumBoardDbContext dbContext) : IBoardRepository
{
    public async Task<BoardSnapshot?> GetSnapshotAsync(
        Guid projectId,
        Guid userId,
        TaskFilter filter,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.AsNoTracking()
            .Where(item => item.Id == projectId && item.Members.Any(member => member.UserId == userId))
            .Select(item => new { item.Id, item.Name, item.BoardVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        var members = await (
            from membership in dbContext.ProjectMembers.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.ProjectId == projectId
            orderby user.Name
            select new BoardMember(user.Id, user.Name, membership.Role))
            .ToListAsync(cancellationToken);

        var columns = await dbContext.Columns.AsNoTracking()
            .Where(column => column.ProjectId == projectId)
            .OrderBy(column => column.Position)
            .Select(column => new { column.Id, column.Name, column.Position, column.Version })
            .ToListAsync(cancellationToken);

        var tasksQuery =
            from task in dbContext.Tasks.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on task.AssigneeId equals user.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            where task.ProjectId == projectId
            select new { task, AssigneeName = assignee == null ? null : assignee.Name };
        if (filter.AssigneeId is not null) tasksQuery = tasksQuery.Where(item => item.task.AssigneeId == filter.AssigneeId);
        if (filter.Priority is not null) tasksQuery = tasksQuery.Where(item => item.task.Priority == filter.Priority);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = PostgreSqlLike.ContainsLiteral(filter.Search);
            tasksQuery = tasksQuery.Where(item => EF.Functions.ILike(
                    item.task.Title, pattern, PostgreSqlLike.EscapeCharacter) ||
                (item.task.Description != null && EF.Functions.ILike(
                    item.task.Description, pattern, PostgreSqlLike.EscapeCharacter)));
        }

        var tasks = await tasksQuery.OrderBy(item => item.task.Position)
            .Select(item => new BoardTask(
                item.task.Id, item.task.ColumnId, item.task.Title, item.task.Description, item.task.Priority,
                item.task.AssigneeId, item.AssigneeName, item.task.DueDate, item.task.Position, item.task.Version,
                item.task.CreatedAt, item.task.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new BoardSnapshot(
            project.Id,
            project.Name,
            project.BoardVersion,
            members,
            columns.Select(column => new BoardColumnItem(
                column.Id,
                column.Name,
                column.Position,
                column.Version,
                tasks.Where(task => task.ColumnId == column.Id).ToList())).ToList());
    }

    public Task<List<BoardMember>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken) =>
        (from membership in dbContext.ProjectMembers.AsNoTracking()
         join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
         where membership.ProjectId == projectId
         orderby user.Name
         select new BoardMember(user.Id, user.Name, membership.Role))
        .ToListAsync(cancellationToken);

    public Task<List<BoardColumn>> GetColumnsAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Columns.Where(column => column.ProjectId == projectId).OrderBy(column => column.Position).ToListAsync(cancellationToken);

    public Task<BoardColumn?> FindColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken) =>
        dbContext.Columns.SingleOrDefaultAsync(column => column.ProjectId == projectId && column.Id == columnId, cancellationToken);

    public Task<bool> ColumnContainsTasksAsync(Guid columnId, CancellationToken cancellationToken) =>
        dbContext.Tasks.AnyAsync(task => task.ColumnId == columnId, cancellationToken);

    public Task<List<TaskItem>> GetTasksAsync(
        Guid projectId,
        Guid columnId,
        Guid? excludedTaskId,
        CancellationToken cancellationToken) =>
        dbContext.Tasks.Where(task => task.ProjectId == projectId && task.ColumnId == columnId && task.Id != excludedTaskId)
            .OrderBy(task => task.Position).ToListAsync(cancellationToken);

    public Task<TaskItem?> FindTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        dbContext.Tasks.SingleOrDefaultAsync(task => task.ProjectId == projectId && task.Id == taskId, cancellationToken);

    public void AddColumn(BoardColumn column) => dbContext.Columns.Add(column);
    public void RemoveColumn(BoardColumn column) => dbContext.Columns.Remove(column);
    public void AddTask(TaskItem task) => dbContext.Tasks.Add(task);
    public void RemoveTask(TaskItem task) => dbContext.Tasks.Remove(task);
}
