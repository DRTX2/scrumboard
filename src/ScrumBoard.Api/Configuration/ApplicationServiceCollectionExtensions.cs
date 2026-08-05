using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Inbound.Projects;
using ScrumBoard.Application.Ports.Inbound.Reports;
using ScrumBoard.Application.Ports.Inbound.Sessions;
using ScrumBoard.Application.UseCases.Boards;
using ScrumBoard.Application.UseCases.Projects;
using ScrumBoard.Application.UseCases.Reports;
using ScrumBoard.Application.UseCases.Sessions;

namespace ScrumBoard.Api.Configuration;

internal static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<ISessionUseCase, SessionUseCase>();
        services.AddScoped<IProjectUseCase, ProjectUseCase>();
        services.AddScoped<IBoardUseCase, BoardUseCase>();
        services.AddScoped<IReportUseCase, ReportUseCase>();
        return services;
    }
}
