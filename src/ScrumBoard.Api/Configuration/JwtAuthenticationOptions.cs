namespace ScrumBoard.Api.Configuration;

internal sealed class JwtAuthenticationOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "ScrumBoard.Api";
    public string Audience { get; init; } = "ScrumBoard.Web";
    public string SigningKey { get; init; } = "development-only-signing-key-change-in-production";
}
