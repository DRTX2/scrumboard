using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Out;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Ordering;
using ScrumBoard.Domain.Primitives;
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
    public async Task<BoardSnapshot> GetAsync(
        Guid projectId,
        TaskFilter filter,
        int taskLimit,
        CancellationToken cancellationToken)
    {
        ValidateProjectAndFilter(projectId, filter);
        InputValidation.Range(taskLimit, 1, 50, nameof(taskLimit));
        EnsureAuthenticated();
        return await boards.GetSnapshotAsync(projectId, currentUser.UserId, Normalize(filter), taskLimit, cancellationToken)
            ?? throw HiddenNotFound();
    }

    public async Task<TaskPage> GetTasksAsync(
        Guid projectId,
        Guid columnId,
        TaskFilter filter,
        int limit,
        long? afterPosition,
        Guid? afterTaskId,
        long expectedBoardVersion,
        CancellationToken cancellationToken)
    {
        ValidateProjectAndFilter(projectId, filter);
        InputValidation.Identifier(columnId, nameof(columnId));
        InputValidation.Range(limit, 1, 50, nameof(limit));
        InputValidation.Positive(expectedBoardVersion, nameof(expectedBoardVersion));
        if (afterPosition.HasValue != afterTaskId.HasValue)
        {
            throw new ValidationException("invalid_page_cursor", "La posición y el identificador del cursor deben enviarse juntos.");
        }

        if (afterPosition is not null) InputValidation.Positive(afterPosition.Value, nameof(afterPosition));
        if (afterTaskId is not null) InputValidation.Identifier(afterTaskId.Value, nameof(afterTaskId));
        EnsureAuthenticated();
        var result = await boards.GetTaskPageAsync(projectId, columnId, currentUser.UserId, Normalize(filter), limit,
            afterPosition, afterTaskId, expectedBoardVersion, cancellationToken) ?? throw HiddenNotFound();
        return result.Page ?? throw ColumnNotFound();
    }

    public async Task<ColumnResult> CreateColumnAsync(Guid projectId, CreateColumn request, CancellationToken cancellationToken)
    {
        InputValidation.Identifier(projectId, nameof(projectId));
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        InputValidation.RequiredText(request.Name, 100, nameof(request.Name));
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
        InputValidation.Identifier(projectId, nameof(projectId));
        InputValidation.Identifier(columnId, nameof(columnId));
        InputValidation.Positive(expectedVersion, nameof(expectedVersion));
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        InputValidation.RequiredText(request.Name, 100, nameof(request.Name));
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
        InputValidation.Identifier(projectId, nameof(projectId));
        InputValidation.Identifier(columnId, nameof(columnId));
        InputValidation.Positive(expectedBoardVersion, nameof(expectedBoardVersion));
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        var project = await RequireMembershipAsync(projectId, true, cancellationToken);
        EnsureVersion(project.BoardVersion, expectedBoardVersion);
        var columns = await boards.GetColumnsAsync(projectId, cancellationToken);
        var column = columns.SingleOrDefault(item => item.Id == columnId) ?? throw ColumnNotFound();
        var siblings = columns.Where(item => item.Id != columnId)
            .OrderBy(item => item.Position).ThenBy(item => item.Id).ToList();
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

    public async Task<long> DeleteColumnAsync(
        Guid projectId,
        Guid columnId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        InputValidation.Identifier(projectId, nameof(projectId));
        InputValidation.Identifier(columnId, nameof(columnId));
        InputValidation.Positive(expectedVersion, nameof(expectedVersion));
        var project = await RequireMembershipAsync(projectId, true, cancellationToken);
        var column = await boards.FindColumnAsync(projectId, columnId, cancellationToken) ?? throw ColumnNotFound();
        EnsureVersion(column.Version, expectedVersion);
        BoardColumn.EnsureCanDelete(await boards.ColumnContainsTasksAsync(columnId, cancellationToken));
        boards.RemoveColumn(column);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishAsync(new ColumnDeletedNotification(projectId, columnId, project.BoardVersion), cancellationToken);
        return project.BoardVersion;
    }

    public async Task<TaskResult> CreateTaskAsync(Guid projectId, CreateTask request, CancellationToken cancellationToken)
    {
        InputValidation.Identifier(projectId, nameof(projectId));
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        InputValidation.Identifier(request.ColumnId, nameof(request.ColumnId));
        InputValidation.Identifier(request.AssigneeId, nameof(request.AssigneeId));
        InputValidation.Defined<TaskPriority>(request.Priority, nameof(request.Priority));
        ValidateTaskText(request.Title, request.Description);
        var project = await RequireMembershipAsync(projectId, false, cancellationToken);
        await EnsureColumnAsync(projectId, request.ColumnId, cancellationToken);
        EnsureAssignee(project, request.AssigneeId);
        var position = await GetAppendTaskPositionAsync(projectId, request.ColumnId, null, cancellationToken);
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
        InputValidation.Identifier(projectId, nameof(projectId));
        InputValidation.Identifier(taskId, nameof(taskId));
        InputValidation.Positive(expectedVersion, nameof(expectedVersion));
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        InputValidation.Identifier(request.AssigneeId, nameof(request.AssigneeId));
        InputValidation.Defined<TaskPriority>(request.Priority, nameof(request.Priority));
        ValidateTaskText(request.Title, request.Description);
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
        InputValidation.Identifier(projectId, nameof(projectId));
        InputValidation.Identifier(taskId, nameof(taskId));
        InputValidation.Positive(expectedBoardVersion, nameof(expectedBoardVersion));
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        InputValidation.Identifier(request.ColumnId, nameof(request.ColumnId));
        var project = await RequireMembershipAsync(projectId, false, cancellationToken);
        EnsureVersion(project.BoardVersion, expectedBoardVersion);
        await EnsureColumnAsync(projectId, request.ColumnId, cancellationToken);
        var task = await boards.FindTaskAsync(projectId, taskId, cancellationToken) ?? throw TaskNotFound();
        var position = await GetMoveTaskPositionAsync(projectId, request.ColumnId, taskId,
            request.BeforeTaskId, request.AfterTaskId, cancellationToken);
        task.Move(request.ColumnId, position, clock.UtcNow);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var response = ToResponse(task, project.BoardVersion);
        await notifier.PublishAsync(new TaskMovedNotification(projectId, response), cancellationToken);
        return response;
    }

    public async Task<long> DeleteTaskAsync(Guid projectId, Guid taskId, long expectedVersion, CancellationToken cancellationToken)
    {
        InputValidation.Identifier(projectId, nameof(projectId));
        InputValidation.Identifier(taskId, nameof(taskId));
        InputValidation.Positive(expectedVersion, nameof(expectedVersion));
        var project = await RequireMembershipAsync(projectId, false, cancellationToken);
        var task = await boards.FindTaskAsync(projectId, taskId, cancellationToken) ?? throw TaskNotFound();
        EnsureVersion(task.Version, expectedVersion);
        boards.RemoveTask(task);
        project.TouchBoard(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.PublishAsync(new TaskDeletedNotification(projectId, taskId, project.BoardVersion), cancellationToken);
        return project.BoardVersion;
    }

    public async Task<IReadOnlyList<BoardMember>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        InputValidation.Identifier(projectId, nameof(projectId));
        EnsureAuthenticated();
        return await boards.GetMembersAsync(projectId, currentUser.UserId, cancellationToken) ?? throw HiddenNotFound();
    }

    private async Task<long> GetAppendTaskPositionAsync(
        Guid projectId,
        Guid columnId,
        Guid? excludedTaskId,
        CancellationToken cancellationToken)
    {
        var maxPosition = await boards.GetMaxTaskPositionAsync(projectId, columnId, excludedTaskId, cancellationToken);
        try
        {
            return OrderPosition.Between(maxPosition, null);
        }
        catch (DomainException exception) when (exception.Code == "order_rebalance_required")
        {
            return await RebalanceTaskPositionAsync(projectId, columnId, excludedTaskId, null, null, cancellationToken);
        }
    }

    private async Task<long> GetMoveTaskPositionAsync(
        Guid projectId,
        Guid columnId,
        Guid taskId,
        Guid? beforeTaskId,
        Guid? afterTaskId,
        CancellationToken cancellationToken)
    {
        if (beforeTaskId is null && afterTaskId is null)
        {
            return await GetAppendTaskPositionAsync(projectId, columnId, taskId, cancellationToken);
        }

        var neighbors = await boards.GetTaskOrderNeighborsAsync(
            projectId, columnId, taskId, beforeTaskId, afterTaskId, cancellationToken);
        if (neighbors is null)
        {
            throw InvalidOrderNeighbors("Los vecinos deben pertenecer a la colección de destino y ser adyacentes.");
        }

        try
        {
            return OrderPosition.Between(neighbors.PreviousPosition, neighbors.NextPosition);
        }
        catch (DomainException exception) when (exception.Code == "order_rebalance_required")
        {
            return await RebalanceTaskPositionAsync(
                projectId, columnId, taskId, beforeTaskId, afterTaskId, cancellationToken);
        }
    }

    private async Task<long> RebalanceTaskPositionAsync(
        Guid projectId,
        Guid columnId,
        Guid? excludedTaskId,
        Guid? beforeTaskId,
        Guid? afterTaskId,
        CancellationToken cancellationToken)
    {
        var siblings = await boards.GetTasksAsync(projectId, columnId, excludedTaskId, cancellationToken);
        var insertIndex = ResolveInsertIndex(siblings.Select(item => item.Id).ToList(), beforeTaskId, afterTaskId);
        return NextPosition(siblings.Select(item => item.Position).ToList(), insertIndex, beforeTaskId, afterTaskId,
            (item, value) => item.Move(item.ColumnId, value, clock.UtcNow), siblings);
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
            throw new ForbiddenException("project_owner_required", "Se requiere el permiso de propietario del proyecto.");
        }

        return project;
    }

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated) throw HiddenNotFound();
    }

    private async Task EnsureColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken)
    {
        if (await boards.FindColumnAsync(projectId, columnId, cancellationToken) is null)
        {
            throw ColumnNotFound();
        }
    }

    private static void EnsureAssignee(Project project, Guid assigneeId)
    {
        if (project.Members.All(member => member.UserId != assigneeId))
        {
            throw new ConflictException("assignee_not_member", "La persona asignada debe pertenecer al proyecto.");
        }
    }

    private static int ResolveInsertIndex(IReadOnlyList<Guid> orderedIds, Guid? beforeId, Guid? afterId)
    {
        if (beforeId is not null && afterId is not null)
        {
            if (beforeId == afterId)
            {
                throw InvalidOrderNeighbors("Los vecinos anterior y posterior deben ser distintos.");
            }

            var beforeIndex = orderedIds.IndexOf(beforeId.Value);
            var afterIndex = orderedIds.IndexOf(afterId.Value);
            if (beforeIndex < 0 || afterIndex < 0)
            {
                throw InvalidOrderNeighbors("Los vecinos deben pertenecer a la colección de destino.");
            }

            if (afterIndex + 1 != beforeIndex)
            {
                throw InvalidOrderNeighbors("Los vecinos indicados no son consecutivos ni están en el orden esperado.");
            }

            return beforeIndex;
        }

        if (beforeId is not null)
        {
            var index = orderedIds.IndexOf(beforeId.Value);
            if (index < 0) throw InvalidOrderNeighbors("El vecino posterior no pertenece a la colección de destino.");
            return index;
        }

        if (afterId is not null)
        {
            var index = orderedIds.IndexOf(afterId.Value);
            if (index < 0) throw InvalidOrderNeighbors("El vecino anterior no pertenece a la colección de destino.");
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

    private static TaskFilter Normalize(TaskFilter filter) => filter with { Search = InputValidation.Search(filter.Search) };
    private static void ValidateProjectAndFilter(Guid projectId, TaskFilter filter)
    {
        InputValidation.Identifier(projectId, nameof(projectId));
        filter = InputValidation.Required(filter, "filter_required", "El filtro es obligatorio.");
        InputValidation.Defined(filter.Priority, nameof(filter.Priority));
        InputValidation.Search(filter.Search);
    }

    private static void ValidateTaskText(string? title, string? description)
    {
        InputValidation.RequiredText(title, 200, nameof(title));
        InputValidation.OptionalText(description, 4_000, nameof(description));
    }

    private static void EnsureVersion(long actual, long expected)
    {
        if (actual != expected) throw new OptimisticConcurrencyException("version_mismatch", "El recurso cambió después de ser leído.");
    }

    private static ColumnResult ToResponse(BoardColumn column, long boardVersion) =>
        new(column.Id, column.ProjectId, column.Name, column.Position, column.Version, boardVersion);
    private static TaskResult ToResponse(TaskItem task, long boardVersion) =>
        new(task.Id, task.ProjectId, task.ColumnId, task.Title, task.Description, task.Priority, task.AssigneeId,
            task.DueDate, task.Position, task.Version, boardVersion, task.CreatedAt, task.UpdatedAt);
    private static ConflictException InvalidOrderNeighbors(string message) => new("invalid_order_neighbors", message);
    private static NotFoundException HiddenNotFound() => new("project_not_found", "No se encontró el proyecto.");
    private static NotFoundException ColumnNotFound() => new("column_not_found", "No se encontró la columna.");
    private static NotFoundException TaskNotFound() => new("task_not_found", "No se encontró la tarea.");
}
