using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Ports.Inbound.Sessions;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Application.UseCases.Sessions;

public sealed class SessionUseCase(IUserRepository users, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer) : ISessionUseCase
{
    public async Task<SessionResponse> CreateAsync(CreateSession request, CancellationToken cancellationToken)
    {
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            throw new ValidationException("email_required", "El correo electrónico es obligatorio.");
        }

        if (email.Length > 254)
        {
            throw new ValidationException("email_too_long", "El correo electrónico no puede superar 254 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("password_required", "La contraseña es obligatoria.");
        }

        if (request.Password.Length > 256)
        {
            throw new ValidationException("password_too_long", "La contraseña no puede superar 256 caracteres.");
        }

        var user = await users.FindByEmailAsync(email.ToLowerInvariant(), cancellationToken);
        var passwordHash = user is { IsActive: true } ? user.PasswordHash : passwordHasher.DummyHash;
        var passwordMatches = passwordHasher.Verify(request.Password, passwordHash);
        if (user is not { IsActive: true } || !passwordMatches)
        {
            throw new AuthenticationFailedException();
        }

        var token = tokenIssuer.Issue(user);
        return new SessionResponse(user.Id, user.Name, user.Email, token.AccessToken, token.ExpiresAt);
    }
}
