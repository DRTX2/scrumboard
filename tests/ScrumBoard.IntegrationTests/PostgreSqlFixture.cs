using DotNet.Testcontainers.Images;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScrumBoard.Infrastructure;
using ScrumBoard.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ScrumBoard.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string? UnavailableReason { get; private set; }
    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("PostgreSQL container is unavailable.");

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:16.4-alpine")
                .WithImagePullPolicy(PullPolicy.Missing)
                .WithCleanUp(false)
                .WithAutoRemove(true)
                .WithDatabase("scrumboard_tests")
                .WithUsername("scrumboard")
                .WithPassword("scrumboard")
                .Build();
            await _container.StartAsync();

            await using var provider = BuildServices();
            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception exception)
        {
            UnavailableReason = $"Docker/PostgreSQL unavailable: {exception.GetType().Name}: {exception.Message}";
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    public ServiceProvider BuildServices()
    {
        EnsureAvailable();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = ConnectionString,
                ["Jwt:Issuer"] = "ScrumBoard.Api",
                ["Jwt:Audience"] = "ScrumBoard.Web",
                ["Jwt:SigningKey"] = "integration-test-signing-key-with-32-characters",
                ["Jwt:LifetimeMinutes"] = "30",
                ["Password:Pepper"] = "scrumboard-development-pepper-only",
                ["Password:Iterations"] = "210000"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    public WebApplicationFactory<Program> CreateApiFactory()
    {
        EnsureAvailable();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", ConnectionString));
    }

    public void EnsureAvailable()
    {
        if (UnavailableReason is not null)
        {
            throw Xunit.Sdk.SkipException.ForSkip(UnavailableReason);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL integration";
}
