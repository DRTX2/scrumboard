using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status,
    ProjectRole Role,
    string Etag,
    DateTimeOffset UpdatedAt);

public sealed record ProjectDetailsResponse(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status,
    ProjectRole Role,
    string Etag,
    string BoardEtag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total, int TotalPages);
public sealed record UserResponse(Guid Id, string Name, ProjectRole Role);
public sealed record AssigneeResponse(Guid Id, string Name);
public sealed record BoardProjectResponse(Guid Id, string Name, string Etag);
public sealed record BoardTaskResponse(
    Guid Id,
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? AssigneeId,
    AssigneeResponse? Assignee,
    DateOnly? DueDate,
    long Position,
    string Etag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
public sealed record BoardColumnResponse(Guid Id, string Name, long Position, string Etag, IReadOnlyList<BoardTaskResponse> Tasks);
public sealed record BoardResponse(
    BoardProjectResponse Project,
    IReadOnlyList<BoardColumnResponse> Columns,
    IReadOnlyList<UserResponse> Members,
    string Etag);
public sealed record ColumnMutationResponse(Guid Id, Guid ProjectId, string Name, long Position, string Etag, string BoardEtag);
public sealed record TaskMutationResponse(
    Guid Id,
    Guid ProjectId,
    Guid ColumnId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? AssigneeId,
    DateOnly? DueDate,
    long Position,
    string Etag,
    string BoardEtag,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal static class ApiResponses
{
    public static ProjectResponse ToResponse(this ProjectSummary project) => new(
        project.Id, project.Name, project.Description, project.StartDate, project.ExpectedEndDate, project.Status,
        project.Role, Etag(project.Version), project.UpdatedAt);

    public static ProjectDetailsResponse ToResponse(this ProjectDetails project) => new(
        project.Id, project.Name, project.Description, project.StartDate, project.ExpectedEndDate, project.Status,
        project.Role, Etag(project.Version), Etag(project.BoardVersion), project.CreatedAt, project.UpdatedAt);

    public static BoardResponse ToResponse(this BoardSnapshot board)
    {
        var members = board.Members.Select(member => new UserResponse(member.UserId, member.Name, member.Role)).ToList();
        var tasksByColumn = board.Columns.ToDictionary(
            column => column.Id,
            column => (IReadOnlyList<BoardTaskResponse>)column.Tasks.Select(task => new BoardTaskResponse(
                task.Id,
                task.ColumnId,
                task.Title,
                task.Description,
                task.Priority,
                task.AssigneeId,
                task.AssigneeId is not null && task.AssigneeName is not null
                    ? new AssigneeResponse(task.AssigneeId.Value, task.AssigneeName)
                    : null,
                task.DueDate,
                task.Position,
                Etag(task.Version),
                task.CreatedAt,
                task.UpdatedAt)).ToList());
        var columns = board.Columns.Select(column => new BoardColumnResponse(
            column.Id, column.Name, column.Position, Etag(column.Version), tasksByColumn[column.Id])).ToList();
        return new BoardResponse(
            new BoardProjectResponse(board.ProjectId, board.ProjectName, Etag(board.BoardVersion)),
            columns,
            members,
            Etag(board.BoardVersion));
    }

    public static ColumnMutationResponse ToResponse(this ColumnResult column) => new(
        column.Id, column.ProjectId, column.Name, column.Position, Etag(column.Version), Etag(column.BoardVersion));

    public static TaskMutationResponse ToResponse(this TaskResult task) => new(
        task.Id, task.ProjectId, task.ColumnId, task.Title, task.Description, task.Priority, task.AssigneeId,
        task.DueDate, task.Position, Etag(task.Version), Etag(task.BoardVersion), task.CreatedAt, task.UpdatedAt);

    private static string Etag(long version) => $"\"{version}\"";
}
