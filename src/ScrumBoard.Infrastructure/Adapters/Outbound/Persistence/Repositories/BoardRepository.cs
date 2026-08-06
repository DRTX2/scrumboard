using System.Data;
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
        int taskLimit,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var project = await dbContext.Projects.AsNoTracking()
            .Where(item => item.Id == projectId && item.Members.Any(member => member.UserId == userId))
            .Select(item => new { item.Id, item.Name, item.BoardVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        var members = await (
            from membership in dbContext.ProjectMembers.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.ProjectId == projectId
            orderby user.Name, user.Id
            select new BoardMember(user.Id, user.Name, membership.Role))
            .ToListAsync(cancellationToken);

        var filteredTasks = ApplyFilter(dbContext.Tasks.AsNoTracking().Where(task => task.ProjectId == projectId), filter);
        var columns = await dbContext.Columns.AsNoTracking()
            .Where(column => column.ProjectId == projectId)
            .OrderBy(column => column.Position)
            .ThenBy(column => column.Id)
            .Select(column => new
            {
                column.Id,
                column.Name,
                column.Position,
                column.Version,
                Total = filteredTasks.LongCount(task => task.ColumnId == column.Id),
                Tasks = (from task in filteredTasks
                         join assignee in dbContext.Users.AsNoTracking() on task.AssigneeId equals assignee.Id
                         where task.ColumnId == column.Id
                         orderby task.Position, task.Id
                         select new BoardTask(
                             task.Id, task.ColumnId, task.Title, task.Description, task.Priority, task.AssigneeId,
                             assignee.Name, task.DueDate, task.Position, task.Version,
                             task.CreatedAt, task.UpdatedAt))
                    .Take(taskLimit + 1)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var columnItems = new List<BoardColumnItem>(columns.Count);
        foreach (var column in columns)
        {
            var tasks = column.Tasks;
            var hasMore = tasks.Count > taskLimit;
            if (hasMore) tasks.RemoveAt(taskLimit);
            columnItems.Add(new BoardColumnItem(
                column.Id,
                column.Name,
                column.Position,
                column.Version,
                tasks,
                column.Total,
                hasMore));
        }

        var snapshot = new BoardSnapshot(
            project.Id,
            project.Name,
            project.BoardVersion,
            members,
            columnItems);
        await transaction.CommitAsync(cancellationToken);
        return snapshot;
    }

    public async Task<TaskPageReadResult?> GetTaskPageAsync(
        Guid projectId,
        Guid columnId,
        Guid userId,
        TaskFilter filter,
        int limit,
        long? afterPosition,
        Guid? afterTaskId,
        long expectedBoardVersion,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var boardVersion = await dbContext.Projects.AsNoTracking()
            .Where(project => project.Id == projectId &&
                project.Members.Any(member => member.UserId == userId))
            .Select(project => (long?)project.BoardVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (boardVersion is null) return null;
        if (boardVersion != expectedBoardVersion)
        {
            throw new ScrumBoard.Application.Errors.OptimisticConcurrencyException(
                "version_mismatch", "El recurso cambió después de ser leído.");
        }

        var filteredTasks = ApplyFilter(dbContext.Tasks.AsNoTracking()
            .Where(task => task.ProjectId == projectId && task.ColumnId == columnId), filter);
        var pageTasks = filteredTasks;
        if (afterPosition is not null && afterTaskId is not null)
        {
            pageTasks = pageTasks.Where(task => task.Position > afterPosition.Value ||
                task.Position == afterPosition.Value && task.Id.CompareTo(afterTaskId.Value) > 0);
        }

        var row = await dbContext.Columns.AsNoTracking()
            .Where(column => column.ProjectId == projectId && column.Id == columnId)
            .Select(column => new
            {
                Total = filteredTasks.LongCount(),
                Items = (from task in pageTasks
                         join assignee in dbContext.Users.AsNoTracking() on task.AssigneeId equals assignee.Id
                         orderby task.Position, task.Id
                         select new BoardTask(
                             task.Id, task.ColumnId, task.Title, task.Description, task.Priority, task.AssigneeId,
                             assignee.Name, task.DueDate, task.Position, task.Version,
                             task.CreatedAt, task.UpdatedAt))
                    .Take(limit + 1)
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return new TaskPageReadResult(null);

        var items = row.Items;
        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(limit);
        var page = new TaskPage(items, row.Total, hasMore, boardVersion.Value);
        await transaction.CommitAsync(cancellationToken);
        return new TaskPageReadResult(page);
    }

    public async Task<List<BoardMember>?> GetMembersAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var members = await (from membership in dbContext.ProjectMembers.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.ProjectId == projectId && dbContext.ProjectMembers
                .Any(requester => requester.ProjectId == projectId && requester.UserId == userId)
            orderby user.Name, user.Id
            select new BoardMember(user.Id, user.Name, membership.Role))
            .ToListAsync(cancellationToken);
        return members.Count == 0 ? null : members;
    }

    public Task<List<BoardColumn>> GetColumnsAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Columns.Where(column => column.ProjectId == projectId)
            .OrderBy(column => column.Position).ThenBy(column => column.Id).ToListAsync(cancellationToken);

    public Task<BoardColumn?> FindColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken) =>
        dbContext.Columns.SingleOrDefaultAsync(column => column.ProjectId == projectId && column.Id == columnId, cancellationToken);

    public Task<bool> ColumnContainsTasksAsync(Guid columnId, CancellationToken cancellationToken) =>
        dbContext.Tasks.AnyAsync(task => task.ColumnId == columnId, cancellationToken);

    public Task<long?> GetMaxTaskPositionAsync(
        Guid projectId,
        Guid columnId,
        Guid? excludedTaskId,
        CancellationToken cancellationToken) =>
        dbContext.Tasks.AsNoTracking()
            .Where(task => task.ProjectId == projectId && task.ColumnId == columnId && task.Id != excludedTaskId)
            .MaxAsync(task => (long?)task.Position, cancellationToken);

    public async Task<TaskOrderNeighbors?> GetTaskOrderNeighborsAsync(
        Guid projectId,
        Guid columnId,
        Guid excludedTaskId,
        Guid? beforeTaskId,
        Guid? afterTaskId,
        CancellationToken cancellationToken)
    {
        var siblings = dbContext.Tasks.AsNoTracking()
            .Where(task => task.ProjectId == projectId && task.ColumnId == columnId && task.Id != excludedTaskId);

        if (beforeTaskId is not null && afterTaskId is not null)
        {
            if (beforeTaskId == afterTaskId) return null;
            var anchors = await siblings
                .Where(task => task.Id == beforeTaskId || task.Id == afterTaskId)
                .OrderBy(task => task.Position)
                .ThenBy(task => task.Id)
                .Select(task => new { task.Id, task.Position })
                .ToListAsync(cancellationToken);
            if (anchors.Count != 2) return null;

            var after = anchors[0];
            var before = anchors[1];
            if (after.Id != afterTaskId || before.Id != beforeTaskId) return null;

            var hasTaskBetween = await siblings.AnyAsync(task =>
                (task.Position > after.Position || task.Position == after.Position && task.Id.CompareTo(after.Id) > 0) &&
                (task.Position < before.Position || task.Position == before.Position && task.Id.CompareTo(before.Id) < 0),
                cancellationToken);
            return hasTaskBetween ? null : new TaskOrderNeighbors(after.Position, before.Position);
        }

        if (beforeTaskId is not null)
        {
            var before = await siblings.Where(task => task.Id == beforeTaskId)
                .Select(task => new { task.Id, task.Position })
                .SingleOrDefaultAsync(cancellationToken);
            if (before is null) return null;
            var previous = await siblings
                .Where(task => task.Position < before.Position ||
                    task.Position == before.Position && task.Id.CompareTo(before.Id) < 0)
                .OrderByDescending(task => task.Position)
                .ThenByDescending(task => task.Id)
                .Select(task => (long?)task.Position)
                .FirstOrDefaultAsync(cancellationToken);
            return new TaskOrderNeighbors(previous, before.Position);
        }

        if (afterTaskId is not null)
        {
            var after = await siblings.Where(task => task.Id == afterTaskId)
                .Select(task => new { task.Id, task.Position })
                .SingleOrDefaultAsync(cancellationToken);
            if (after is null) return null;
            var next = await siblings
                .Where(task => task.Position > after.Position ||
                    task.Position == after.Position && task.Id.CompareTo(after.Id) > 0)
                .OrderBy(task => task.Position)
                .ThenBy(task => task.Id)
                .Select(task => (long?)task.Position)
                .FirstOrDefaultAsync(cancellationToken);
            return new TaskOrderNeighbors(after.Position, next);
        }

        throw new ArgumentException("At least one task neighbor is required.");
    }

    public Task<List<TaskItem>> GetTasksAsync(
        Guid projectId,
        Guid columnId,
        Guid? excludedTaskId,
        CancellationToken cancellationToken) =>
        dbContext.Tasks.Where(task => task.ProjectId == projectId && task.ColumnId == columnId && task.Id != excludedTaskId)
            .OrderBy(task => task.Position).ThenBy(task => task.Id).ToListAsync(cancellationToken);

    public Task<TaskItem?> FindTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        dbContext.Tasks.SingleOrDefaultAsync(task => task.ProjectId == projectId && task.Id == taskId, cancellationToken);

    public void AddColumn(BoardColumn column) => dbContext.Columns.Add(column);
    public void RemoveColumn(BoardColumn column) => dbContext.Columns.Remove(column);
    public void AddTask(TaskItem task) => dbContext.Tasks.Add(task);
    public void RemoveTask(TaskItem task) => dbContext.Tasks.Remove(task);

    private static IQueryable<TaskItem> ApplyFilter(IQueryable<TaskItem> tasks, TaskFilter filter)
    {
        if (filter.AssigneeId is not null) tasks = tasks.Where(task => task.AssigneeId == filter.AssigneeId);
        if (filter.Priority is not null) tasks = tasks.Where(task => task.Priority == filter.Priority);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = PostgreSqlLike.ContainsLiteral(filter.Search);
            tasks = tasks.Where(task => EF.Functions.ILike(task.Title, pattern, PostgreSqlLike.EscapeCharacter) ||
                task.Description != null && EF.Functions.ILike(
                    task.Description, pattern, PostgreSqlLike.EscapeCharacter));
        }

        return tasks;
    }
}
