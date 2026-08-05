using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Ports.Inbound.Sessions;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Application.UseCases.Sessions;

public sealed class SessionUseCase(IUserRepository users, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer) : ISessionUseCase
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
