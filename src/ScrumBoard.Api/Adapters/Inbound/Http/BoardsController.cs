using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Api.Adapters.Inbound.Http.Contracts;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Adapters.Inbound.Http;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}")]
public sealed class BoardsController(IBoardUseCase boards) : ControllerBase
{
    [HttpGet("board")]
    public async Task<ActionResult<BoardResponse>> Get(
        Guid projectId,
        [FromQuery] Guid? assigneeId,
        [FromQuery] TaskPriority? priority,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var board = await boards.GetAsync(projectId, new TaskFilter(assigneeId, priority, search), cancellationToken);
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
