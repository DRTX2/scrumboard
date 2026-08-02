using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Boards;

public sealed record BoardFilter(Guid? AssigneeId = null, TaskPriority? Priority = null, string? Search = null);
public sealed record BoardMember(Guid UserId, string Name, ProjectRole Role);
public sealed record BoardTask(
    Guid Id,
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    DateOnly? DueDate,
    long Position,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
public sealed record BoardColumnItem(Guid Id, string Name, long Position, long Version, IReadOnlyList<BoardTask> Tasks);
public sealed record BoardSnapshot(
    Guid ProjectId,
    string ProjectName,
    long BoardVersion,
    IReadOnlyList<BoardMember> Members,
    IReadOnlyList<BoardColumnItem> Columns);

public sealed record CreateColumn(string Name);
public sealed record UpdateColumn(string Name);
public sealed record MoveColumn(Guid? BeforeColumnId, Guid? AfterColumnId);
public sealed record ColumnResponse(Guid Id, Guid ProjectId, string Name, long Position, long Version, long BoardVersion);

public sealed record CreateTask(
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? AssigneeId,
    DateOnly? DueDate);
public sealed record UpdateTask(string Title, string? Description, TaskPriority Priority, Guid? AssigneeId, DateOnly? DueDate);
public sealed record MoveTask(Guid ColumnId, Guid? BeforeTaskId, Guid? AfterTaskId);
public sealed record TaskResponse(
    Guid Id,
    Guid ProjectId,
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? AssigneeId,
    DateOnly? DueDate,
    long Position,
    long Version,
    long BoardVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
