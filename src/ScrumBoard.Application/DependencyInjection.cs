using Microsoft.Extensions.DependencyInjection;
using ScrumBoard.Application.Boards;
using ScrumBoard.Application.Projects;
using ScrumBoard.Application.Reports;
using ScrumBoard.Application.Sessions;

namespace ScrumBoard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SessionService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<BoardService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
