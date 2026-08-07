using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using ScrumBoard.Adapters.Outbound.Reporting;
using ScrumBoard.Application.Ports.Out;

namespace ScrumBoard.Adapters.Outbound.Configuration;

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
