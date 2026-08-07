namespace ScrumBoard.Application.Ports.Inbound.Sessions;

public sealed record SessionResponse(
    Guid UserId,
    string Name,
    string Email,
    string AccessToken,
    DateTimeOffset ExpiresAt);
