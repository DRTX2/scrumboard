using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using ScrumBoard.Application.Abstractions;
using ScrumBoard.Infrastructure.Persistence;
using ScrumBoard.Infrastructure.Persistence.Repositories;
using ScrumBoard.Infrastructure.Reports;
using ScrumBoard.Infrastructure.Security;
using ScrumBoard.Infrastructure.Time;

namespace ScrumBoard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContextPool<ScrumBoardDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ScrumBoardDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            }));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IReportDataSource, ReportDataSource>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ScrumBoardDbContext>());
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IReportExporter, PdfReportExporter>();
        services.AddSingleton<IReportExporter, ExcelReportExporter>();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => options.SigningKey.Length >= 32, "JWT signing key must contain at least 32 characters.")
            .Validate(options => options.LifetimeMinutes is >= 5 and <= 120, "JWT lifetime must be between 5 and 120 minutes.")
            .ValidateOnStart();
        services.AddOptions<PasswordOptions>().Bind(configuration.GetSection(PasswordOptions.SectionName))
            .Validate(options => options.Pepper.Length >= 16, "Password pepper must contain at least 16 characters.")
            .Validate(options => options.Iterations >= 100_000, "PBKDF2 iterations must be at least 100000.")
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtRequiredFieldsValidator>();

        QuestPDF.Settings.License = LicenseType.Community;
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
