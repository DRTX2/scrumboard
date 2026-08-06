using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ScrumBoard.IntegrationTests.Adapters.Outbound.Persistence;

namespace ScrumBoard.IntegrationTests.Adapters.Inbound.Http;

[Collection(PostgreSqlCollection.Name)]
public sealed class MaintenanceModeApiTests(PostgreSqlFixture database)
{
    [DockerFact]
    public async Task MaintenanceMode_ReturnsSpanishProblemAndAllowsHealthChecks()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("MaintenanceMode", "true"));
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var maintenanceResponse = await client.GetAsync("/api/v1/projects");
        var body = await maintenanceResponse.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        using var liveResponse = await client.GetAsync("/health/live");
        using var readyResponse = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, maintenanceResponse.StatusCode);
        Assert.Equal("application/problem+json", maintenanceResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TimeSpan.FromSeconds(60), maintenanceResponse.Headers.RetryAfter?.Delta);
        Assert.Equal("Servicio temporalmente no disponible.", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("maintenance_mode", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.4",
            problem.RootElement.GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
    }
}
