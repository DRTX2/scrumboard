using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ScrumBoard.Adapters.Inbound.Http;
using ScrumBoard.Adapters.Inbound.Infrastructure;
using ScrumBoard.Adapters.Inbound.SignalR;
using ScrumBoard.Application.Ports.Out;

namespace ScrumBoard.Adapters.Inbound;

public static class InboundAdapterExtensions
{
    public static IServiceCollection AddInboundAdapters(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddBoardSignalR();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services
            .AddControllers()
            .AddApplicationPart(typeof(BoardsController).Assembly)
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.ValidationState == ModelValidationState.Invalid)
                    .ToDictionary(
                        entry => entry.Key,
                        _ => ApiValidationMessages.InvalidValue);
                var problem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "La solicitud no es válida.",
                    Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1"
                };
                problem.Extensions["code"] = "invalid_request";
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                return new ContentResult
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentType = "application/problem+json",
                    Content = JsonSerializer.Serialize(problem)
                };
            };
        });
        return services;
    }

    public static IApplicationBuilder UseInboundRequestContext(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseInboundIdempotency(this IApplicationBuilder app)
    {
        app.UseMiddleware<IdempotencyMiddleware>();
        return app;
    }

    public static IEndpointRouteBuilder MapInboundEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        endpoints.MapHub<BoardHub>("/hubs/boards");
        return endpoints;
    }
}
