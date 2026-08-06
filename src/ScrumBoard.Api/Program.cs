using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using ScrumBoard.Api.Adapters.Outbound.Persistence;
using ScrumBoard.Api.Adapters.SignalR;
using ScrumBoard.Api.Configuration;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Api.Infrastructure.Idempotency;
using ScrumBoard.Application.Context;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Infrastructure.Configuration;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;
using ScrumBoard.Infrastructure.Adapters.Outbound.Security;

var builder = WebApplication.CreateBuilder(args);
var maintenanceMode = builder.Configuration.GetValue<bool>("MaintenanceMode");
var forwardedHeadersEnabled = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
var maintenanceProblemJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        // Container Apps is the only public path to the container; trust its final ingress hop.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddApplicationUseCases();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IIdempotencyCoordinator, PostgreSqlIdempotencyCoordinator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddBoardSignalR();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.Services.Configure<ApiBehaviorOptions>(options =>
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                // Query strings can be logged, so only accept SignalR's fallback token on the exact hub endpoint.
                if (!string.IsNullOrEmpty(token) && context.Request.Path == "/hubs/boards")
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await AuthenticationProblemResponses.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Se requiere autenticación.",
                    "authentication_required",
                    "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2");
            },
            OnForbidden = async context =>
            {
                await AuthenticationProblemResponses.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Acceso denegado.",
                    "access_denied",
                    "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4");
            }
        };
    });
builder.Services.AddAuthorization();
var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length == 0)
    {
        policy.SetIsOriginAllowed(_ => false);
        return;
    }

    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders(
            "Content-Disposition",
            "ETag",
            "X-Board-ETag",
            "X-Total-Count",
            "Location",
            "Idempotency-Replayed");
}));
builder.Services.AddRateLimiter(options =>
{
    var problemJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "10";
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Demasiadas solicitudes.",
            Detail = "Reintente la solicitud después del intervalo indicado por Retry-After.",
            Type = "https://www.rfc-editor.org/rfc/rfc6585#section-4"
        };
        problem.Extensions["code"] = "rate_limit_exceeded";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problem, problemJsonOptions),
            cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var subject = context.User.FindFirst("sub")?.Value;
        var partitionKey = string.IsNullOrWhiteSpace(subject)
            ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"}"
            : $"sub:{subject}";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "ScrumBoard API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT access token returned by POST /api/v1/sessions."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
    .AddDbContextCheck<ScrumBoardDbContext>("postgresql", tags: ["ready"]);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ScrumBoard.Api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation());

var app = builder.Build();

if (forwardedHeadersEnabled) app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    if (!maintenanceMode || context.Request.Path == "/health/live" || context.Request.Path == "/health/ready")
    {
        await next(context);
        return;
    }

    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    context.Response.ContentType = "application/problem+json";
    context.Response.Headers.RetryAfter = "60";
    var problem = new ProblemDetails
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Title = "Servicio temporalmente no disponible.",
        Detail = "El servicio está en mantenimiento. Reintente después del intervalo indicado por Retry-After.",
        Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.4"
    };
    problem.Extensions["code"] = "maintenance_mode";
    problem.Extensions["traceId"] = context.TraceIdentifier;
    await context.Response.WriteAsync(
        JsonSerializer.Serialize(problem, maintenanceProblemJsonOptions),
        context.RequestAborted);
});
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseSwagger();
app.MapScalarApiReference(options => options.WithTitle("ScrumBoard API").WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapControllers();
app.MapHub<BoardHub>("/hubs/boards");

app.Run();

public partial class Program;
