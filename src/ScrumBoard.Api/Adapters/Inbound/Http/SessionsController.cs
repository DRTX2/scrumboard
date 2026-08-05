using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Application.Ports.Inbound.Sessions;

namespace ScrumBoard.Api.Adapters.Inbound.Http;

[ApiController]
[Route("api/v1/sessions")]
public sealed class SessionsController(ISessionUseCase sessions) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Create(CreateSession request, CancellationToken cancellationToken) =>
        Ok(await sessions.CreateAsync(request, cancellationToken));
}
