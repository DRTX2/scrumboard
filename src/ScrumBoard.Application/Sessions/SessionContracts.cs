namespace ScrumBoard.Application.Sessions;

public sealed record CreateSession(string Email, string Password);
public sealed record SessionToken(string AccessToken, DateTimeOffset ExpiresAt);
public sealed record SessionResponse(Guid UserId, string Name, string Email, string AccessToken, DateTimeOffset ExpiresAt);
