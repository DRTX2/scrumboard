using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Api.Adapters.Inbound.Http.Contracts;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Ports.Inbound.Boards;

namespace ScrumBoard.Api.Adapters.Inbound.Http;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/tasks")]
public sealed class TasksController(IBoardUseCase boards) : ControllerBase
{
    [HttpPost]
    [Idempotent]
    public async Task<ActionResult<TaskMutationResponse>> Create(
        Guid projectId,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await boards.CreateTaskAsync(projectId, request.ToCommand(), cancellationToken);
        WriteTags(task);
        return Created($"/api/v1/projects/{projectId}/tasks/{task.Id}", task.ToResponse());
    }

    [HttpPut("{taskId:guid}")]
    public async Task<ActionResult<TaskMutationResponse>> Update(
        Guid projectId,
        Guid taskId,
        UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await boards.UpdateTaskAsync(projectId, taskId, request.ToCommand(), EntityTags.Require(Request), cancellationToken);
        WriteTags(task);
        return Ok(task.ToResponse());
    }

    [HttpPatch("{taskId:guid}")]
    public async Task<ActionResult<TaskMutationResponse>> Move(
        Guid projectId,
        Guid taskId,
        MoveTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await boards.MoveTaskAsync(projectId, taskId, request.ToCommand(), EntityTags.Require(Request), cancellationToken);
        WriteTags(task);
        return Ok(task.ToResponse());
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var boardVersion = await boards.DeleteTaskAsync(projectId, taskId, EntityTags.Require(Request), cancellationToken);
        Response.Headers["X-Board-ETag"] = EntityTags.Format(boardVersion);
        return NoContent();
    }

    private void WriteTags(TaskResult task)
    {
        EntityTags.Write(Response, task.Version);
        Response.Headers["X-Board-ETag"] = EntityTags.Format(task.BoardVersion);
    }
}
