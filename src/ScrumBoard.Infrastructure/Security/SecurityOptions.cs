namespace ScrumBoard.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int LifetimeMinutes { get; init; } = 30;
}

public sealed class PasswordOptions
{
    public const string SectionName = "Password";
    public string Pepper { get; init; } = string.Empty;
    public int Iterations { get; init; } = 210_000;
}
