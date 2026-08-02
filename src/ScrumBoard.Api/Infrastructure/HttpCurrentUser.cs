using System.IdentityModel.Tokens.Jwt;
using ScrumBoard.Application.Abstractions;

namespace ScrumBoard.Api.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id
        : Guid.Empty;

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated is true && UserId != Guid.Empty;
}
