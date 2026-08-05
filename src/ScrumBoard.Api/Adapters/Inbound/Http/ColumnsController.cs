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
    [HttpPost]
    [Idempotent]
    public async Task<ActionResult<ColumnMutationResponse>> Create(
        Guid projectId,
        CreateColumn request,
        CancellationToken cancellationToken)
    {
        var column = await boards.CreateColumnAsync(projectId, request, cancellationToken);
        EntityTags.Write(Response, column.Version);
        Response.Headers["X-Board-ETag"] = EntityTags.Format(column.BoardVersion);
        return Created($"/api/v1/projects/{projectId}/columns/{column.Id}", column.ToResponse());
    }

    [HttpPut("{columnId:guid}")]
    public async Task<ActionResult<ColumnMutationResponse>> Update(
        Guid projectId,
        Guid columnId,
        UpdateColumn request,
        CancellationToken cancellationToken)
    {
        var column = await boards.UpdateColumnAsync(projectId, columnId, request, EntityTags.Require(Request), cancellationToken);
        WriteTags(column);
        return Ok(column.ToResponse());
    }

    [HttpPatch("{columnId:guid}")]
    public async Task<ActionResult<ColumnMutationResponse>> Move(
        Guid projectId,
        Guid columnId,
        MoveColumn request,
        CancellationToken cancellationToken)
    {
        var column = await boards.MoveColumnAsync(projectId, columnId, request, EntityTags.Require(Request), cancellationToken);
        WriteTags(column);
        return Ok(column.ToResponse());
    }

    [HttpDelete("{columnId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid columnId, CancellationToken cancellationToken)
    {
        await boards.DeleteColumnAsync(projectId, columnId, EntityTags.Require(Request), cancellationToken);
        return NoContent();
    }

    private void WriteTags(ColumnResult column)
    {
        EntityTags.Write(Response, column.Version);
        Response.Headers["X-Board-ETag"] = EntityTags.Format(column.BoardVersion);
    }
}
