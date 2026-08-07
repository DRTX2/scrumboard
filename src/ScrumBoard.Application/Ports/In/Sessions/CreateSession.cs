namespace ScrumBoard.Application.Ports.Inbound.Sessions;

public sealed record CreateSession(string Email, string Password);
