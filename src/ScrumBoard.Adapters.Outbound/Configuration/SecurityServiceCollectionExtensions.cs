using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ScrumBoard.Adapters.Outbound.Security;
using ScrumBoard.Adapters.Outbound.Time;
using ScrumBoard.Application.Ports.Out;

namespace ScrumBoard.Adapters.Outbound.Configuration;

internal static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<PasswordOptions>().Bind(configuration.GetSection(PasswordOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IValidateOptions<PasswordOptions>, PasswordOptionsValidator>();
        return services;
    }
}
