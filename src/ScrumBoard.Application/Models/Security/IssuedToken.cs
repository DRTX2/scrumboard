namespace ScrumBoard.Application.Models.Security;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);
