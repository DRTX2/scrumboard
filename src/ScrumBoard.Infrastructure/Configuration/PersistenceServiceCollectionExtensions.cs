using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Repositories;

namespace ScrumBoard.Infrastructure.Configuration;

internal static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceAdapter(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContextPool<ScrumBoardDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ScrumBoardDbContext).Assembly.FullName)));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IReportDataSource, ReportDataSource>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ScrumBoardDbContext>());
        return services;
    }
}
