using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Inbound.Projects;
using ScrumBoard.Application.Ports.Inbound.Sessions;

namespace ScrumBoard.Adapters.Inbound.Http.Contracts;

internal static class ApiRequestMappings
{
    public static CreateColumn ToCommand(this CreateColumnRequest request) => new(request.Name);
    public static UpdateColumn ToCommand(this UpdateColumnRequest request) => new(request.Name);
    public static MoveColumn ToCommand(this MoveColumnRequest request) => new(request.BeforeColumnId, request.AfterColumnId);
    public static CreateTask ToCommand(this CreateTaskRequest request) => new(
        request.ColumnId, request.Title, request.Description, request.Priority, request.AssigneeId, request.DueDate);
    public static UpdateTask ToCommand(this UpdateTaskRequest request) => new(
        request.Title, request.Description, request.Priority, request.AssigneeId, request.DueDate);
    public static MoveTask ToCommand(this MoveTaskRequest request) =>
        new(request.ColumnId, request.BeforeTaskId, request.AfterTaskId);
    public static CreateProject ToCommand(this CreateProjectRequest request) => new(
        request.Name, request.Description, request.StartDate, request.ExpectedEndDate, request.Status);
    public static UpdateProject ToCommand(this UpdateProjectRequest request) => new(
        request.Name, request.Description, request.StartDate, request.ExpectedEndDate, request.Status);
    public static ProjectListQuery ToQuery(this ProjectListRequest request) =>
        new(request.Page, request.PageSize, request.Search, request.Sort, request.Direction);
    public static CreateSession ToCommand(this CreateSessionRequest request) => new(request.Email, request.Password);
    public static TaskFilter ToFilter(this BoardQueryRequest request) =>
        new(request.AssigneeId, request.Priority, request.Search);
    public static TaskFilter ToFilter(this TaskPageQueryRequest request) =>
        new(request.AssigneeId, request.Priority, request.Search);
    public static TaskFilter ToFilter(this ReportQueryRequest request) =>
        new(request.AssigneeId, request.Priority, request.Search);
}
