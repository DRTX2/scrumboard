using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ScrumBoard.Application.Models.Security;
using ScrumBoard.Application.Ports.Out;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.Adapters.Outbound.Security;

internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public IssuedToken Issue(User user)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
