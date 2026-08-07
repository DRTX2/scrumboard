using ScrumBoard.Application.Ports.Out;

namespace ScrumBoard.Adapters.Inbound.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id)
        ? id
        : Guid.Empty;

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated is true && UserId != Guid.Empty;
}
