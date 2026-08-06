using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Api.Adapters.Inbound.Http.Contracts;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Ports.Inbound.Boards;

namespace ScrumBoard.Api.Adapters.Inbound.Http;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/columns")]
public sealed class ColumnsController(IBoardUseCase boards) : ControllerBase
{
    [HttpGet("{columnId:guid}/tasks")]
    public async Task<ActionResult<TaskPageResponse>> Tasks(
        Guid projectId,
        Guid columnId,
        [FromQuery] TaskPageQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var page = await boards.GetTasksAsync(projectId, columnId, query.ToFilter(),
            query.Limit, query.AfterPosition, query.AfterTaskId, EntityTags.Require(Request), cancellationToken);
        EntityTags.Write(Response, page.BoardVersion);
        return Ok(page.ToResponse());
    }

    [HttpPost]
    [Idempotent]
    public async Task<ActionResult<ColumnMutationResponse>> Create(
        Guid projectId,
        CreateColumnRequest request,
        CancellationToken cancellationToken)
    {
        var column = await boards.CreateColumnAsync(projectId, request.ToCommand(), cancellationToken);
        EntityTags.Write(Response, column.Version);
        Response.Headers["X-Board-ETag"] = EntityTags.Format(column.BoardVersion);
        return Created($"/api/v1/projects/{projectId}/columns/{column.Id}", column.ToResponse());
    }

    [HttpPut("{columnId:guid}")]
    public async Task<ActionResult<ColumnMutationResponse>> Update(
        Guid projectId,
        Guid columnId,
        UpdateColumnRequest request,
        CancellationToken cancellationToken)
    {
        var column = await boards.UpdateColumnAsync(projectId, columnId, request.ToCommand(), EntityTags.Require(Request), cancellationToken);
        WriteTags(column);
        return Ok(column.ToResponse());
    }

    [HttpPatch("{columnId:guid}")]
    public async Task<ActionResult<ColumnMutationResponse>> Move(
        Guid projectId,
        Guid columnId,
        MoveColumnRequest request,
        CancellationToken cancellationToken)
    {
        var column = await boards.MoveColumnAsync(projectId, columnId, request.ToCommand(), EntityTags.Require(Request), cancellationToken);
        WriteTags(column);
        return Ok(column.ToResponse());
    }

    [HttpDelete("{columnId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid columnId, CancellationToken cancellationToken)
    {
        var boardVersion = await boards.DeleteColumnAsync(projectId, columnId, EntityTags.Require(Request), cancellationToken);
        Response.Headers["X-Board-ETag"] = EntityTags.Format(boardVersion);
        return NoContent();
    }

    private void WriteTags(ColumnResult column)
    {
        EntityTags.Write(Response, column.Version);
        Response.Headers["X-Board-ETag"] = EntityTags.Format(column.BoardVersion);
    }
}
