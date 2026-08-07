using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScrumBoard.Adapters.Outbound.Persistence;

namespace ScrumBoard.Adapters.Outbound.Configuration;

public static class OutboundAdapterServiceCollectionExtensions
{
    public static IServiceCollection AddOutboundAdapters(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddPersistenceAdapter(configuration)
            .AddSecurityAdapters(configuration)
            .AddReportExporters();
    }

    public static IHealthChecksBuilder AddPostgreSqlAdapterHealthCheck(this IHealthChecksBuilder healthChecks) =>
        healthChecks.AddDbContextCheck<ScrumBoardDbContext>("postgresql", tags: ["ready"]);
}
