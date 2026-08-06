using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Repositories;

namespace ScrumBoard.Infrastructure.Configuration;

internal static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceAdapter(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
        services.AddDbContextPool<ScrumBoardDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Database, npgsql =>
                npgsql.MigrationsAssembly(typeof(ScrumBoardDbContext).Assembly.FullName)));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IReportDataSource, ReportDataSource>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ScrumBoardDbContext>());
        return services;
    }
}
