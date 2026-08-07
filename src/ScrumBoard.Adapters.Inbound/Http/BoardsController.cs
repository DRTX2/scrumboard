using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Adapters.Inbound.Http.Contracts;
using ScrumBoard.Adapters.Inbound.Infrastructure;
using ScrumBoard.Application.Ports.Inbound.Boards;

namespace ScrumBoard.Adapters.Inbound.Http;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}")]
public sealed class BoardsController(IBoardUseCase boards) : ControllerBase
{
    [HttpGet("board")]
    public async Task<ActionResult<BoardResponse>> Get(
        Guid projectId,
        [FromQuery] BoardQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var board = await boards.GetAsync(projectId, query.ToFilter(), query.TaskLimit, cancellationToken);
        EntityTags.Write(Response, board.BoardVersion);
        return Ok(board.ToResponse());
    }

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> Members(Guid projectId, CancellationToken cancellationToken)
    {
        var members = await boards.GetMembersAsync(projectId, cancellationToken);
        return Ok(members.Select(member => new UserResponse(member.UserId, member.Name, member.Role)).ToList());
    }
}
