using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Api.Contracts;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Boards;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}")]
public sealed class BoardsController(BoardService boards) : ControllerBase
{
    [HttpGet("board")]
    public async Task<ActionResult<BoardResponse>> Get(
        Guid projectId,
        [FromQuery] Guid? assigneeId,
        [FromQuery] TaskPriority? priority,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var board = await boards.GetAsync(projectId, new BoardFilter(assigneeId, priority, search), cancellationToken);
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
