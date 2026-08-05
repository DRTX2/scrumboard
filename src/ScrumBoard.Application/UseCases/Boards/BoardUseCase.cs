using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Context;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Primitives;
using ScrumBoard.Domain.Ordering;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.UseCases.Boards;

public sealed class BoardUseCase(
    IProjectRepository projects,
    IBoardRepository boards,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    IBoardNotifier notifier) : IBoardUseCase
{
    public async Task<BoardSnapshot> GetAsync(Guid projectId, TaskFilter filter, CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(projectId, false, cancellationToken);
        return await boards.GetSnapshotAsync(projectId, currentUser.UserId, Normalize(filter), cancellationToken)
            ?? throw HiddenNotFound();
    }

    public async Task<ColumnResult> CreateColumnAsync(Guid projectId, CreateColumn request, CancellationToken cancellationToken)
    {
        var project = await RequireMembershipAsync(projectId, true, cancellationToken);
        var columns = await boards.GetColumnsAsync(projectId, cancellationToken);
        var position = NextPosition<BoardColumn>(columns.Select(column => column.Position).ToList(), columns.Count, null, null,
            (column, value) => column.MoveTo(value, clock.UtcNow));
        var column = new BoardColumn(Guid.NewGuid(), projectId, request.Name, position, clock.UtcNow);
        boards.AddColumn(column);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = ToResponse(column, project.BoardVersion);
        await notifier.PublishAsync(new ColumnChangedNotification(projectId, response), cancellationToken);
        return response;
    }

    public async Task<ColumnResult> UpdateColumnAsync(
        Guid projectId,
        Guid columnId,
        UpdateColumn request,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var project = await RequireMembershipAsync(projectId, true, cancellationToken);
        var column = await boards.FindColumnAsync(projectId, columnId, cancellationToken) ?? throw ColumnNotFound();
        EnsureVersion(column.Version, expectedVersion);
        column.Update(request.Name, clock.UtcNow);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = ToResponse(column, project.BoardVersion);
        await notifier.PublishAsync(new ColumnChangedNotification(projectId, response), cancellationToken);
        return response;
    }

    public async Task<ColumnResult> MoveColumnAsync(
        Guid projectId,
        Guid columnId,
        MoveColumn request,
        long expectedBoardVersion,
        CancellationToken cancellationToken)
    {
        var project = await RequireMembershipAsync(projectId, true, cancellationToken);
        EnsureVersion(project.BoardVersion, expectedBoardVersion);
        var columns = await boards.GetColumnsAsync(projectId, cancellationToken);
        var column = columns.SingleOrDefault(item => item.Id == columnId) ?? throw ColumnNotFound();
        var siblings = columns.Where(item => item.Id != columnId).OrderBy(item => item.Position).ToList();
        var insertIndex = ResolveInsertIndex(siblings.Select(item => item.Id).ToList(), request.BeforeColumnId, request.AfterColumnId);
        var position = NextPosition(siblings.Select(item => item.Position).ToList(), insertIndex, request.BeforeColumnId, request.AfterColumnId,
            (item, value) => item.MoveTo(value, clock.UtcNow), siblings);
        column.MoveTo(position, clock.UtcNow);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = ToResponse(column, project.BoardVersion);
        await notifier.PublishAsync(new ColumnChangedNotification(projectId, response), cancellationToken);
        return response;
    }

    public async Task DeleteColumnAsync(
        Guid projectId,
        Guid columnId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var project = await RequireMembershipAsync(projectId, true, cancellationToken);
        var column = await boards.FindColumnAsync(projectId, columnId, cancellationToken) ?? throw ColumnNotFound();
        EnsureVersion(column.Version, expectedVersion);
        BoardColumn.EnsureCanDelete(await boards.ColumnContainsTasksAsync(columnId, cancellationToken));
        boards.RemoveColumn(column);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishAsync(new ColumnDeletedNotification(projectId, columnId, project.BoardVersion), cancellationToken);
    }

    public async Task<TaskResult> CreateTaskAsync(Guid projectId, CreateTask request, CancellationToken cancellationToken)
    {
        var project = await RequireMembershipAsync(projectId, false, cancellationToken);
        await EnsureColumnAsync(projectId, request.ColumnId, cancellationToken);
        EnsureAssignee(project, request.AssigneeId);
        var tasks = await boards.GetTasksAsync(projectId, request.ColumnId, null, cancellationToken);
        var position = NextPosition<TaskItem>(tasks.Select(task => task.Position).ToList(), tasks.Count, null, null,
            (task, value) => task.Move(task.ColumnId, value, clock.UtcNow));
        var task = new TaskItem(Guid.NewGuid(), projectId, request.ColumnId, request.Title, request.Description,
            request.Priority, request.AssigneeId, request.DueDate, position, clock.UtcNow);
        boards.AddTask(task);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = ToResponse(task, project.BoardVersion);
        await notifier.PublishAsync(new TaskCreatedNotification(projectId, response), cancellationToken);
        return response;
    }

    public async Task<TaskResult> UpdateTaskAsync(
        Guid projectId,
        Guid taskId,
        UpdateTask request,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var project = await RequireMembershipAsync(projectId, false, cancellationToken);
        var task = await boards.FindTaskAsync(projectId, taskId, cancellationToken) ?? throw TaskNotFound();
        EnsureVersion(task.Version, expectedVersion);
        EnsureAssignee(project, request.AssigneeId);
        task.Update(request.Title, request.Description, request.Priority, request.AssigneeId, request.DueDate, clock.UtcNow);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = ToResponse(task, project.BoardVersion);
        await notifier.PublishAsync(new TaskUpdatedNotification(projectId, response), cancellationToken);
        return response;
    }

    public async Task<TaskResult> MoveTaskAsync(
        Guid projectId,
        Guid taskId,
        MoveTask request,
        long expectedBoardVersion,
        CancellationToken cancellationToken)
    {
        var project = await RequireMembershipAsync(projectId, false, cancellationToken);
        EnsureVersion(project.BoardVersion, expectedBoardVersion);
        await EnsureColumnAsync(projectId, request.ColumnId, cancellationToken);
        var task = await boards.FindTaskAsync(projectId, taskId, cancellationToken) ?? throw TaskNotFound();
        var siblings = await boards.GetTasksAsync(projectId, request.ColumnId, taskId, cancellationToken);
        var insertIndex = ResolveInsertIndex(siblings.Select(item => item.Id).ToList(), request.BeforeTaskId, request.AfterTaskId);
        var position = NextPosition(siblings.Select(item => item.Position).ToList(), insertIndex, request.BeforeTaskId, request.AfterTaskId,
            (item, value) => item.Move(item.ColumnId, value, clock.UtcNow), siblings);
        task.Move(request.ColumnId, position, clock.UtcNow);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = ToResponse(task, project.BoardVersion);
        await notifier.PublishAsync(new TaskMovedNotification(projectId, response), cancellationToken);
        return response;
    }

    public async Task DeleteTaskAsync(Guid projectId, Guid taskId, long expectedVersion, CancellationToken cancellationToken)
    {
        var project = await RequireMembershipAsync(projectId, false, cancellationToken);
        var task = await boards.FindTaskAsync(projectId, taskId, cancellationToken) ?? throw TaskNotFound();
        EnsureVersion(task.Version, expectedVersion);
        boards.RemoveTask(task);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishAsync(new TaskDeletedNotification(projectId, taskId, project.BoardVersion), cancellationToken);
    }

    public async Task<IReadOnlyList<BoardMember>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await RequireMembershipAsync(projectId, false, cancellationToken);
        return await boards.GetMembersAsync(projectId, cancellationToken);
    }

    private async Task<Project> RequireMembershipAsync(Guid projectId, bool ownerRequired, CancellationToken cancellationToken)
    {
        var project = await projects.FindAsync(projectId, cancellationToken);
        var membership = project?.Members.SingleOrDefault(member => member.UserId == currentUser.UserId);
        if (project is null || !currentUser.IsAuthenticated || membership is null)
        {
            throw HiddenNotFound();
        }

        if (ownerRequired && membership.Role is not ProjectRole.Owner)
        {
            throw new ForbiddenException("project_owner_required", "Project owner permission is required.");
        }

        return project;
    }

    private async Task EnsureColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken)
    {
        if (await boards.FindColumnAsync(projectId, columnId, cancellationToken) is null)
        {
            throw ColumnNotFound();
        }
    }

    private static void EnsureAssignee(Project project, Guid? assigneeId)
    {
        if (assigneeId is not null && project.Members.All(member => member.UserId != assigneeId))
        {
            throw new ConflictException("assignee_not_member", "The assignee must be a project member.");
        }
    }

    private static int ResolveInsertIndex(IReadOnlyList<Guid> orderedIds, Guid? beforeId, Guid? afterId)
    {
        if (beforeId is not null)
        {
            var index = orderedIds.IndexOf(beforeId.Value);
            if (index < 0) throw new ConflictException("invalid_order_neighbor", "The before item is not in the target collection.");
            return index;
        }

        if (afterId is not null)
        {
            var index = orderedIds.IndexOf(afterId.Value);
            if (index < 0) throw new ConflictException("invalid_order_neighbor", "The after item is not in the target collection.");
            return index + 1;
        }

        return orderedIds.Count;
    }

    private static long NextPosition<T>(
        IReadOnlyList<long> positions,
        int insertIndex,
        Guid? beforeId,
        Guid? afterId,
        Action<T, long> move,
        IReadOnlyList<T>? items = null)
    {
        try
        {
            return OrderPosition.Between(
                insertIndex > 0 ? positions[insertIndex - 1] : null,
                insertIndex < positions.Count ? positions[insertIndex] : null);
        }
        catch (DomainException exception) when (exception.Code == "order_rebalance_required" && items is not null)
        {
            var rebalanced = OrderPosition.Rebalance(items.Count);
            for (var index = 0; index < items.Count; index++) move(items[index], rebalanced[index]);
            return OrderPosition.Between(
                insertIndex > 0 ? rebalanced[insertIndex - 1] : null,
                insertIndex < rebalanced.Count ? rebalanced[insertIndex] : null);
        }
    }

    private static TaskFilter Normalize(TaskFilter filter) => filter with { Search = filter.Search?.Trim() };
    private static void EnsureVersion(long actual, long expected)
    {
        if (actual != expected) throw new OptimisticConcurrencyException("version_mismatch", "The resource changed after it was read.");
    }

    private static ColumnResult ToResponse(BoardColumn column, long boardVersion) =>
        new(column.Id, column.ProjectId, column.Name, column.Position, column.Version, boardVersion);
    private static TaskResult ToResponse(TaskItem task, long boardVersion) =>
        new(task.Id, task.ProjectId, task.ColumnId, task.Title, task.Description, task.Priority, task.AssigneeId,
            task.DueDate, task.Position, task.Version, boardVersion, task.CreatedAt, task.UpdatedAt);
    private static NotFoundException HiddenNotFound() => new("project_not_found", "The project was not found.");
    private static NotFoundException ColumnNotFound() => new("column_not_found", "The column was not found.");
    private static NotFoundException TaskNotFound() => new("task_not_found", "The task was not found.");
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> source, T value)
    {
        for (var index = 0; index < source.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(source[index], value)) return index;
        }

        return -1;
    }
}
