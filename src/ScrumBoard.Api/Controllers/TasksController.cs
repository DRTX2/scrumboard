using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Api.Contracts;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Boards;

namespace ScrumBoard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/tasks")]
public sealed class TasksController(BoardService boards) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TaskMutationResponse>> Create(
        Guid projectId,
        CreateTask request,
        CancellationToken cancellationToken)
    {
        var task = await boards.CreateTaskAsync(projectId, request, cancellationToken);
        WriteTags(task);
        return Created($"/api/v1/projects/{projectId}/tasks/{task.Id}", task.ToResponse());
    }

    [HttpPut("{taskId:guid}")]
    public async Task<ActionResult<TaskMutationResponse>> Update(
        Guid projectId,
        Guid taskId,
        UpdateTask request,
        CancellationToken cancellationToken)
    {
        var task = await boards.UpdateTaskAsync(projectId, taskId, request, EntityTags.Require(Request), cancellationToken);
        WriteTags(task);
        return Ok(task.ToResponse());
    }

    [HttpPatch("{taskId:guid}")]
    public async Task<ActionResult<TaskMutationResponse>> Move(
        Guid projectId,
        Guid taskId,
        MoveTask request,
        CancellationToken cancellationToken)
    {
        var task = await boards.MoveTaskAsync(projectId, taskId, request, EntityTags.Require(Request), cancellationToken);
        WriteTags(task);
        return Ok(task.ToResponse());
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        await boards.DeleteTaskAsync(projectId, taskId, EntityTags.Require(Request), cancellationToken);
        return NoContent();
    }

    private void WriteTags(TaskResponse task)
    {
        EntityTags.Write(Response, task.Version);
        Response.Headers["X-Board-ETag"] = EntityTags.Format(task.BoardVersion);
    }
}
