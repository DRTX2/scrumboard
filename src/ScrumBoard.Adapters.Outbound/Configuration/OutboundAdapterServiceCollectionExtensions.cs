using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
}
