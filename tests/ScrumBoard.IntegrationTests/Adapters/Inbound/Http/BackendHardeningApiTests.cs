using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ScrumBoard.IntegrationTests.Adapters.Outbound.Persistence;

namespace ScrumBoard.IntegrationTests.Adapters.Inbound.Http;

[Collection(PostgreSqlCollection.Name)]
public sealed class BackendHardeningApiTests(PostgreSqlFixture database)
{
    private static readonly Guid DemoOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoProjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private const string AllowedOrigin = "http://localhost:4200";
    private static readonly string[] ExposedHeaders =
    [
        "Content-Disposition",
        "ETag",
        "X-Board-ETag",
        "X-Total-Count",
        "Location",
        "Idempotency-Replayed"
    ];

    [DockerFact]
    public async Task NumericJsonEnum_IsRejectedWithStableSpanishProblem()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = AuthenticatedClient(factory);
        using var content = JsonContent(
            """
            {
              "name": "Proyecto inválido",
              "description": null,
              "startDate": "2026-08-05",
              "expectedEndDate": "2026-08-06",
              "status": 2
            }
            """);

        var response = await client.PostAsync("/api/v1/projects", content);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_request", body);
        Assert.Equal("La solicitud no es válida.", problem.RootElement.GetProperty("title").GetString());
    }

    [DockerFact]
    public async Task TaskWithoutAssignee_IsRejectedByHttpModelValidation()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = AuthenticatedClient(factory);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var content = JsonContent(
            """
            {
              "columnId": "30000000-0000-0000-0000-000000000001",
              "title": "Tarea sin responsable",
              "description": null,
              "priority": "medium",
              "dueDate": null
            }
            """);

        var response = await client.PostAsync($"/api/v1/projects/{DemoProjectId}/tasks", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_request", await response.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Cors_PreflightAndActualResponseAllowConfiguredOriginAndExposeApiHeaders()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/projects");
        preflight.Headers.Add("Origin", AllowedOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization");

        using var preflightResponse = await client.SendAsync(preflight);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(DemoOwnerId));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, preflightResponse.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(preflightResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("GET", preflightResponse.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        var exposed = string.Join(',', response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.All(ExposedHeaders, header => Assert.Contains(header, exposed, StringComparison.OrdinalIgnoreCase));
    }

    [DockerFact]
    public async Task Cors_WithoutConfiguredOrigins_DoesNotAllowCrossOriginResponse()
    {
        database.EnsureAvailable();
        await using var factory = CreateApiFactoryWithoutCors();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Expose-Headers"));
    }

    [DockerFact]
    public async Task RateLimiter_UsesAuthenticatedSubjectPartitionsAndReturnsProblemJson()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var firstUser = AuthenticatedClient(factory, DemoOwnerId);
        using var secondUser = AuthenticatedClient(factory, Guid.NewGuid());

        for (var requestNumber = 0; requestNumber < 120; requestNumber++)
        {
            using var accepted = await firstUser.GetAsync("/health/live");
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        using var rejected = await firstUser.GetAsync("/health/live");
        using var otherPartition = await secondUser.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, otherPartition.StatusCode);
    }

    [DockerFact]
    public async Task AccessTokenQuery_IsAcceptedOnlyOnExactHubPath()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));

        var exactHubToken = await ReadQueryTokenAsync(options, scheme, "/hubs/boards");
        var childPathToken = await ReadQueryTokenAsync(options, scheme, "/hubs/boards/extra");
        var apiToken = await ReadQueryTokenAsync(options, scheme, "/api/v1/projects");

        Assert.Equal("query-token", exactHubToken);
        Assert.Null(childPathToken);
        Assert.Null(apiToken);
    }

    private static async Task<string?> ReadQueryTokenAsync(
        JwtBearerOptions options,
        AuthenticationScheme scheme,
        string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString("?access_token=query-token");
        var context = new MessageReceivedContext(httpContext, scheme, options);

        await options.Events.MessageReceived(context);

        return context.Token;
    }

    private static HttpClient AuthenticatedClient(
        WebApplicationFactory<Program> factory,
        Guid? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId ?? DemoOwnerId));
        return client;
    }

    private static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    private WebApplicationFactory<Program> CreateApiFactoryWithoutCors()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.ConnectionString);
            builder.UseSetting("Jwt:Issuer", "ScrumBoard.Api");
            builder.UseSetting("Jwt:Audience", "ScrumBoard.Web");
            builder.UseSetting("Jwt:SigningKey", PostgreSqlFixture.JwtSigningKey);
            builder.UseSetting("Jwt:LifetimeMinutes", "30");
            builder.UseSetting("Password:Pepper", "integration-test-pepper-with-32-characters");
            builder.UseSetting("Password:Iterations", "210000");
            builder.UseSetting("Cors:AllowedOrigins:0", " ");
            builder.UseSetting("Cors:AllowedOrigins:1", " ");
        });
    }

    private static string CreateToken(Guid userId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PostgreSqlFixture.JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            "ScrumBoard.Api",
            "ScrumBoard.Web",
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, "Test User")
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
