using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Api.Adapters.SignalR;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoardSignalR(this IServiceCollection services)
    {
        services.AddSignalR().AddJsonProtocol(options =>
            options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
        services.AddSingleton<BoardPresence>();
        services.AddScoped<PostCommitActionQueue>();
        services.AddScoped<IBoardNotifier, SignalRBoardNotifier>();
        return services;
    }
}
