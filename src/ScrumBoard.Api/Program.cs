using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Api.Realtime;
using ScrumBoard.Application;
using ScrumBoard.Application.Abstractions;
using ScrumBoard.Infrastructure;
using ScrumBoard.Infrastructure.Persistence;
using ScrumBoard.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<BoardPresence>();
builder.Services.AddSingleton<IBoardNotifier, SignalRBoardNotifier>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request validation failed.",
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1"
        };
        problem.Extensions["code"] = "invalid_request";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem);
    };
});

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
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
            if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/boards"))
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
                "Authentication is required.",
                "authentication_required",
                "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2");
        },
        OnForbidden = async context =>
        {
            await AuthenticationProblemResponses.WriteAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "Access is denied.",
                "access_denied",
                "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "10";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Detail = "Retry the request after the interval indicated by Retry-After.",
            Type = "https://www.rfc-editor.org/rfc/rfc6585#section-4"
        }, cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
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

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
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
