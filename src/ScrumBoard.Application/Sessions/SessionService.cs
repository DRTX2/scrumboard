using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Common;

namespace ScrumBoard.Application.Sessions;

public sealed class SessionService(IUserRepository users, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer)
{
    public async Task<SessionResponse> CreateAsync(CreateSession request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationFailedException();
        }

        var token = tokenIssuer.Issue(user);
        return new SessionResponse(user.Id, user.Name, user.Email, token.AccessToken, token.ExpiresAt);
    }
}
