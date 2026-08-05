using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ScrumBoard.Api.Adapters.Outbound.Persistence;
using ScrumBoard.Api.Infrastructure.Idempotency;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Inbound.Projects;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Models;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Seed;
using ScrumBoard.Infrastructure.Adapters.Outbound.Security;

namespace ScrumBoard.IntegrationTests.Adapters.Outbound.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlPersistenceTests(PostgreSqlFixture database)
{
    private static readonly string[] ExpectedColumnNames = ["Backlog", "In progress", "Done"];
    private static readonly string[] ExpectedMigrationNames =
    [
        "CreateUsers",
        "CreateProjects",
        "CreateBoard",
        "CreateIdempotencyRecords",
        "SeedDemoUsers",
        "SeedDemoWorkspace",
        "AddSearchIndexes",
        "AddIdempotencyReplayHeaders",
        "HardenIdempotencyRecords"
    ];
    private static readonly string[] ExpectedSearchIndexes =
    [
        "ix_projects_name_trgm",
        "ix_tasks_description_trgm",
        "ix_tasks_title_trgm"
    ];
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
        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();
        var searchIndexes = await dbContext.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE indexname LIKE 'ix_%_trgm'").ToArrayAsync();
        var extensions = await dbContext.Database.SqlQueryRaw<string>(
            "SELECT extname AS \"Value\" FROM pg_extension WHERE extname = 'pg_trgm'").ToArrayAsync();
        var tables = await dbContext.Database.SqlQueryRaw<string>(
            "SELECT tablename AS \"Value\" FROM pg_tables WHERE tablename = 'idempotency_records'").ToArrayAsync();
        var replayHeaderColumns = await dbContext.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns " +
            "WHERE table_name = 'idempotency_records' AND column_name IN ('etag', 'board_etag')").ToArrayAsync();
        var idempotencyIndexes = await dbContext.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = 'idempotency_records'").ToArrayAsync();
        var responseBodyTypes = await dbContext.Database.SqlQueryRaw<string>(
            "SELECT data_type AS \"Value\" FROM information_schema.columns " +
            "WHERE table_name = 'idempotency_records' AND column_name = 'response_body'").ToArrayAsync();

        Assert.Empty(pendingMigrations);
        Assert.Equal(ExpectedMigrationNames,
            appliedMigrations.Select(migration => migration[(migration.IndexOf('_') + 1)..]));
        Assert.Equal(ExpectedSearchIndexes, searchIndexes.Order());
        Assert.Contains("pg_trgm", extensions);
        Assert.Contains("idempotency_records", tables);
        Assert.Equal(["board_etag", "etag"], replayHeaderColumns.Order());
        Assert.Contains("ux_idempotency_user_key", idempotencyIndexes);
        Assert.DoesNotContain("ux_idempotency_user_operation_key", idempotencyIndexes);
        Assert.Equal("text", Assert.Single(responseBodyTypes));
        Assert.Equal(2, await dbContext.Users.CountAsync());
        Assert.Equal(3, await dbContext.Columns.CountAsync());
        Assert.Equal(3, await dbContext.Tasks.CountAsync());
    }

    [DockerFact]
    public async Task HardenIdempotencyMigration_RoundTripsStoredJsonResponse()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        var record = new IdempotencyRecordRow(Guid.NewGuid(), DemoOwnerId, "POST:/migration-test",
            Guid.NewGuid().ToString("N"), new string('C', 64), DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(24));
        record.Complete(201, "application/json", "{\"message\":\"ok\"}", null, null, null,
            DateTimeOffset.UtcNow);
        dbContext.IdempotencyRecords.Add(record);
        await dbContext.SaveChangesAsync();

        try
        {
            await migrator.MigrateAsync("20260805035300_AddIdempotencyReplayHeaders");
            await migrator.MigrateAsync("20260805042120_HardenIdempotencyRecords");
            dbContext.ChangeTracker.Clear();

            var responseBody = await dbContext.IdempotencyRecords
                .Where(item => item.Id == record.Id)
                .Select(item => item.ResponseBody)
                .SingleAsync();
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"message\":\"ok\"}"), JsonNode.Parse(responseBody!)));
        }
        finally
        {
            await migrator.MigrateAsync("20260805042120_HardenIdempotencyRecords");
            await dbContext.IdempotencyRecords.Where(item => item.Id == record.Id).ExecuteDeleteAsync();
        }
    }

    [DockerFact]
    public async Task BootstrapAdminSeeder_ReconcilesCloudCredentialsAndDisablesDemoMember()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        const string cloudPepper = "cloud-test-pepper-with-32-characters";
        const string cloudPassword = "CloudPassword-2026!";

        try
        {
            await BootstrapAdminSeeder.ApplyAsync(dbContext, "Cloud Owner", "Cloud.Owner@Example.com",
                cloudPassword, cloudPepper, disableDemoMember: true, removeDemoWorkspace: false);
            dbContext.ChangeTracker.Clear();

            var owner = await dbContext.Users.SingleAsync(user => user.Id == DemoOwnerId);
            var memberActive = await dbContext.Users
                .Where(user => user.Id == Guid.Parse("10000000-0000-0000-0000-000000000002"))
                .Select(user => user.IsActive)
                .SingleAsync();
            var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordOptions { Pepper = cloudPepper }));

            Assert.Equal("Cloud Owner", owner.Name);
            Assert.Equal("cloud.owner@example.com", owner.Email);
            Assert.True(hasher.Verify(cloudPassword, owner.PasswordHash));
            Assert.False(memberActive);
        }
        finally
        {
            await BootstrapAdminSeeder.ApplyAsync(dbContext, "Demo Owner", "owner@scrumboard.local",
                "ScrumBoard123!", "scrumboard-development-pepper-only", disableDemoMember: false,
                removeDemoWorkspace: false);
        }
    }

    [DockerFact]
    public async Task ProjectRepository_SearchesCaseInsensitivelyAndReturnsMembershipRole()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

        var result = await repository.ListAsync(DemoOwnerId,
            new ProjectSearchCriteria(1, 20, "sCRUMbOARD", ProjectSortField.Name, SortDirection.Ascending), default);

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
            new TaskFilter(Priority: TaskPriority.Critical, Search: "COLLABORATIVE"), default);

        Assert.NotNull(snapshot);
        Assert.Equal(ExpectedColumnNames, snapshot.Columns.Select(column => column.Name));
        var task = Assert.Single(snapshot.Columns.SelectMany(column => column.Tasks));
        Assert.Equal("Build collaborative board", task.Title);
        Assert.Equal("Demo Member", task.AssigneeName);
        Assert.Empty(snapshot.Columns[0].Tasks);
        Assert.Empty(snapshot.Columns[2].Tasks);
    }

    [DockerFact]
    public async Task ReportDataSource_WithNoMatchingTasks_ReturnsProjectWithEmptyTaskList()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<IReportDataSource>();

        var report = await dataSource.GetAsync(DemoProjectId, new TaskFilter(Search: "does-not-exist"),
            DateTimeOffset.UtcNow, default);

        Assert.NotNull(report);
        Assert.Equal("ScrumBoard Launch", report.ProjectName);
        Assert.Empty(report.Tasks);
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

    [DockerFact]
    public async Task Api_RepeatedIdempotentPost_ReplaysTheCreatedProject()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateOwnerToken());
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var request = new CreateProject("Idempotent project", null, new DateOnly(2026, 8, 4),
            new DateOnly(2026, 8, 11), ProjectStatus.Active);

        var firstResponse = await client.PostAsJsonAsync("/api/v1/projects", request);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var replayResponse = await client.PostAsJsonAsync("/api/v1/projects", request);
        var replayBody = await replayResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        Assert.True(replayResponse.Headers.TryGetValues("Idempotency-Replayed", out var values));
        Assert.Contains("true", values);
        Assert.Equal(firstResponse.Headers.ETag, replayResponse.Headers.ETag);
        Assert.Equal(firstResponse.Headers.Location, replayResponse.Headers.Location);
        Assert.Equal(firstResponse.Content.Headers.ContentType, replayResponse.Content.Headers.ContentType);
        Assert.Equal(firstBody, replayBody);

        var conflictResponse = await client.PostAsJsonAsync("/api/v1/projects", request with { Name = "Different" });
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        var crossOperationResponse = await client.PostAsJsonAsync($"/api/v1/projects/{DemoProjectId}/tasks",
            new CreateTask(Guid.Parse("30000000-0000-0000-0000-000000000001"), "Different operation", null,
                TaskPriority.Low, null, null));
        Assert.Equal(HttpStatusCode.Conflict, crossOperationResponse.StatusCode);

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        var routeKey = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("Idempotency-Key", routeKey);
        var routeRequest = new CreateTask(
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            $"Route fingerprint {Guid.NewGuid():N}", null, TaskPriority.Low, null, null);
        var firstRouteResponse = await client.PostAsJsonAsync($"/api/v1/projects/{DemoProjectId}/tasks", routeRequest);
        var otherRouteResponse = await client.PostAsJsonAsync($"/api/v1/projects/{Guid.NewGuid()}/tasks", routeRequest);

        Assert.Equal(HttpStatusCode.Created, firstRouteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, otherRouteResponse.StatusCode);

        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        Assert.Equal(1, await dbContext.Projects.CountAsync(project => project.Name == request.Name));
        await dbContext.Tasks.Where(task => task.Title == routeRequest.Title).ExecuteDeleteAsync();
        await dbContext.IdempotencyRecords.Where(record => record.Key == routeKey).ExecuteDeleteAsync();
    }

    [DockerFact]
    public async Task Api_IdempotentTaskPost_ReplaysEntityAndBoardEtags()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(DemoOwnerId, "Demo Owner"));
        var idempotencyKey = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        var request = new CreateTask(
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            $"Idempotent task {Guid.NewGuid():N}",
            null,
            TaskPriority.High,
            DemoOwnerId,
            null);

        var firstResponse = await client.PostAsJsonAsync($"/api/v1/projects/{DemoProjectId}/tasks", request);
        var replayResponse = await client.PostAsJsonAsync($"/api/v1/projects/{DemoProjectId}/tasks", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(firstResponse.Headers.ETag, replayResponse.Headers.ETag);
        Assert.Equal(firstResponse.Headers.GetValues("X-Board-ETag"), replayResponse.Headers.GetValues("X-Board-ETag"));
        Assert.True(replayResponse.Headers.Contains("Idempotency-Replayed"));

        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        Assert.Equal(1, await dbContext.Tasks.CountAsync(task => task.Title == request.Title));
        await dbContext.Tasks.Where(task => task.Title == request.Title).ExecuteDeleteAsync();
        await dbContext.IdempotencyRecords.Where(record => record.Key == idempotencyKey).ExecuteDeleteAsync();
    }

    [DockerFact]
    public async Task Api_ReportAuthorizationAndEmptyFilters_AreHandledByApplication()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(DemoOwnerId, "Demo Owner"));

        var emptyReport = await client.GetAsync(
            $"/api/v1/projects/{DemoProjectId}/reports?format=xlsx&search=does-not-exist");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "Outsider"));
        var hiddenReport = await client.GetAsync($"/api/v1/projects/{DemoProjectId}/reports?format=xlsx");

        Assert.Equal(HttpStatusCode.OK, emptyReport.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            emptyReport.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await emptyReport.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, hiddenReport.StatusCode);
    }

    [DockerFact]
    public async Task Api_ProjectVersionErrors_RetainPublicHttpContract()
    {
        database.EnsureAvailable();
        await using var factory = database.CreateApiFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(DemoOwnerId, "Demo Owner"));
        var update = new UpdateProject("ScrumBoard Launch", null, new DateOnly(2026, 7, 30),
            new DateOnly(2026, 8, 30), ProjectStatus.Active);

        var missingTag = await client.PutAsJsonAsync($"/api/v1/projects/{DemoProjectId}", update);
        using var staleRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/projects/{DemoProjectId}")
        {
            Content = JsonContent.Create(update)
        };
        staleRequest.Headers.TryAddWithoutValidation("If-Match", "\"999\"");
        var staleTag = await client.SendAsync(staleRequest);

        Assert.Equal((HttpStatusCode)428, missingTag.StatusCode);
        Assert.Contains("if_match_required", await missingTag.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleTag.StatusCode);
        Assert.Contains("etag_mismatch", await staleTag.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task IdempotencyCoordinator_AbortRollsBackSavedBusinessChangesAndReleasesReservation()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        var coordinator = new PostgreSqlIdempotencyCoordinator(dbContext,
            provider.GetRequiredService<IServiceScopeFactory>());
        var key = Guid.NewGuid().ToString("N");
        var project = new Project(Guid.NewGuid(), "Rolled back project", null, new DateOnly(2026, 8, 4),
            new DateOnly(2026, 8, 11), ProjectStatus.Active, DemoOwnerId, DateTimeOffset.UtcNow);
        var reservation = await coordinator.ReserveAsync(DemoOwnerId, "POST:/test", key, new string('A', 64),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(24), default);

        await coordinator.BeginExecutionAsync(default);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        await coordinator.AbortAsync(reservation.Id, default);

        Assert.False(await dbContext.Projects.AnyAsync(item => item.Id == project.Id));
        Assert.False(await dbContext.IdempotencyRecords.AnyAsync(item => item.Id == reservation.Id));
    }

    [DockerFact]
    public async Task IdempotencyCoordinator_AbortAfterCommitPreservesMutationAndReplayRecord()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        var coordinator = new PostgreSqlIdempotencyCoordinator(dbContext,
            provider.GetRequiredService<IServiceScopeFactory>());
        var key = Guid.NewGuid().ToString("N");
        var project = new Project(Guid.NewGuid(), "Committed project", null, new DateOnly(2026, 8, 4),
            new DateOnly(2026, 8, 11), ProjectStatus.Active, DemoOwnerId, DateTimeOffset.UtcNow);
        var reservation = await coordinator.ReserveAsync(DemoOwnerId, "POST:/test", key, new string('B', 64),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(24), default);

        await coordinator.BeginExecutionAsync(default);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        await coordinator.CompleteAndCommitAsync(reservation.Id,
            new IdempotentResponse(201, "application/json", "{}", "/test", "\"1\"", null),
            DateTimeOffset.UtcNow, default);
        await coordinator.AbortAsync(reservation.Id, default);

        Assert.True(await dbContext.Projects.AnyAsync(item => item.Id == project.Id));
        Assert.True(await dbContext.IdempotencyRecords.AnyAsync(item => item.Id == reservation.Id && item.CompletedAt != null));
        await dbContext.Projects.Where(item => item.Id == project.Id).ExecuteDeleteAsync();
        await dbContext.IdempotencyRecords.Where(item => item.Id == reservation.Id).ExecuteDeleteAsync();
    }

    private static string CreateOwnerToken() => CreateToken(DemoOwnerId, "Demo Owner");

    private static string CreateToken(Guid userId, string name)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PostgreSqlFixture.JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            "ScrumBoard.Api",
            "ScrumBoard.Web",
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, name)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
