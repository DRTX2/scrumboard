using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Infrastructure.Adapters.Outbound.Reporting;

namespace ScrumBoard.Infrastructure.Configuration;

internal static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReportExporters(this IServiceCollection services)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IReportExporter, PdfReportExporter>();
        services.AddSingleton<IReportExporter, ExcelReportExporter>();
        return services;
    }
}
