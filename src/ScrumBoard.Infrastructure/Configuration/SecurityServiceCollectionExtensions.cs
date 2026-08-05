using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Infrastructure.Adapters.Outbound.Security;
using ScrumBoard.Infrastructure.Adapters.Outbound.Time;

namespace ScrumBoard.Infrastructure.Configuration;

internal static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => options.SigningKey.Length >= 32, "JWT signing key must contain at least 32 characters.")
            .Validate(options => options.LifetimeMinutes is >= 5 and <= 120, "JWT lifetime must be between 5 and 120 minutes.")
            .ValidateOnStart();
        services.AddOptions<PasswordOptions>().Bind(configuration.GetSection(PasswordOptions.SectionName))
            .Validate(options => options.Pepper.Length >= 16, "Password pepper must contain at least 16 characters.")
            .Validate(options => options.Iterations >= 100_000, "PBKDF2 iterations must be at least 100000.")
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtRequiredFieldsValidator>();
        return services;
    }
}

internal sealed class JwtRequiredFieldsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options) =>
        string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience)
            ? ValidateOptionsResult.Fail("JWT issuer and audience are required.")
            : ValidateOptionsResult.Success;
}
