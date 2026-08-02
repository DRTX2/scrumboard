using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Boards;
using ScrumBoard.Application.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Infrastructure.Persistence;

namespace ScrumBoard.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlIntegrationTests(PostgreSqlFixture database)
{
    private static readonly string[] ExpectedColumnNames = ["Backlog", "In progress", "Done"];
    private static readonly Guid DemoOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoProjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [DockerFact]
    public async Task Migrations_ApplyCompletelyAndSeedExpectedWorkspace()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();

        Assert.Empty(pendingMigrations);
        Assert.NotEmpty(appliedMigrations);
        Assert.Equal(2, await dbContext.Users.CountAsync());
        Assert.Equal(3, await dbContext.Columns.CountAsync());
        Assert.Equal(3, await dbContext.Tasks.CountAsync());
    }

    [DockerFact]
    public async Task ProjectRepository_SearchesCaseInsensitivelyAndReturnsMembershipRole()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

        var result = await repository.ListAsync(DemoOwnerId,
            new ProjectListQuery(1, 20, "sCRUMbOARD", "name", "asc"), default);

        var project = Assert.Single(result.Items);
        Assert.Equal(DemoProjectId, project.Id);
        Assert.Equal("ScrumBoard Launch", project.Name);
        Assert.Equal(1, result.TotalCount);
    }

    [DockerFact]
    public async Task BoardRepository_AppliesSearchAndPriorityWhilePreservingEmptyColumns()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();

        var snapshot = await repository.GetSnapshotAsync(DemoProjectId, DemoOwnerId,
            new BoardFilter(Priority: TaskPriority.Critical, Search: "COLLABORATIVE"), default);

        Assert.NotNull(snapshot);
        Assert.Equal(ExpectedColumnNames, snapshot.Columns.Select(column => column.Name));
        var task = Assert.Single(snapshot.Columns.SelectMany(column => column.Tasks));
        Assert.Equal("Build collaborative board", task.Title);
        Assert.Equal("Demo Member", task.AssigneeName);
        Assert.Empty(snapshot.Columns[0].Tasks);
        Assert.Empty(snapshot.Columns[2].Tasks);
    }

    [DockerFact]
    public async Task Api_ReadyHealthCheck_UsesMigratedPostgreSqlDatabase()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Api_ProtectedEndpointWithoutToken_ReturnsUnauthorized()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
