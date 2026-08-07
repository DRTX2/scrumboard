using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Projects;

namespace ScrumBoard.Adapters.Inbound.Http.Contracts;

internal static class ApiResponseMappings
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
        var columns = board.Columns.Select(column => new BoardColumnResponse(
            column.Id,
            column.Name,
            column.Position,
            Etag(column.Version),
            column.Tasks.Select(ToResponse).ToList(),
            column.TaskTotal,
            column.HasMoreTasks)).ToList();
        return new BoardResponse(
            new BoardProjectResponse(board.ProjectId, board.ProjectName, Etag(board.BoardVersion)),
            columns,
            members,
            Etag(board.BoardVersion));
    }

    public static TaskPageResponse ToResponse(this TaskPage page) =>
        new(page.Items.Select(ToResponse).ToList(), page.Total, page.HasMore, Etag(page.BoardVersion));

    public static ColumnMutationResponse ToResponse(this ColumnResult column) => new(
        column.Id, column.ProjectId, column.Name, column.Position, Etag(column.Version), Etag(column.BoardVersion));

    public static TaskMutationResponse ToResponse(this TaskResult task) => new(
        task.Id, task.ProjectId, task.ColumnId, task.Title, task.Description, task.Priority, task.AssigneeId,
        task.DueDate, task.Position, Etag(task.Version), Etag(task.BoardVersion), task.CreatedAt, task.UpdatedAt);

    private static BoardTaskResponse ToResponse(BoardTask task) => new(
        task.Id,
        task.ColumnId,
        task.Title,
        task.Description,
        task.Priority,
        task.AssigneeId,
        new AssigneeResponse(task.AssigneeId, task.AssigneeName),
        task.DueDate,
        task.Position,
        Etag(task.Version),
        task.CreatedAt,
        task.UpdatedAt);

    private static string Etag(long version) => $"\"{version}\"";
}
