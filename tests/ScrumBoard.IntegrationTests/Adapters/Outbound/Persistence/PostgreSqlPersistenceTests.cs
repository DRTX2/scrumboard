using System.IdentityModel.Tokens.Jwt;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using ScrumBoard.Api.Adapters.Outbound.Persistence;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Api.Infrastructure.Idempotency;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Inbound.Projects;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Application.UseCases.Reports;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Models;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Repositories;
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
        "HardenIdempotencyRecords",
        "RequireTaskAssigneeAndAddChecks"
    ];
    private static readonly string[] ExpectedSearchIndexes =
    [
        "ix_projects_name_trgm",
        "ix_tasks_description_trgm",
        "ix_tasks_title_trgm"
    ];
    private static readonly Guid DemoOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoProjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();

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
            await migrator.MigrateAsync("20260806021724_RequireTaskAssigneeAndAddChecks");
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
            new TaskFilter(Priority: TaskPriority.Critical, Search: "COLLABORATIVE"), 20, default);

        Assert.NotNull(snapshot);
        Assert.Equal(ExpectedColumnNames, snapshot.Columns.Select(column => column.Name));
        var task = Assert.Single(snapshot.Columns.SelectMany(column => column.Tasks));
        Assert.Equal("Build collaborative board", task.Title);
        Assert.Equal("Demo Member", task.AssigneeName);
        Assert.Empty(snapshot.Columns[0].Tasks);
        Assert.Empty(snapshot.Columns[2].Tasks);
        Assert.Equal(0, snapshot.Columns[0].TaskTotal);
        Assert.Equal(1, snapshot.Columns[1].TaskTotal);
        Assert.False(snapshot.Columns[1].HasMoreTasks);
    }

    [DockerFact]
    public async Task BoardRepository_UsesBoundedKeysetPagesWithIdenticalFilters()
    {
        database.EnsureAvailable();
        var marker = $"page-marker-{Guid.NewGuid():N}";
        var columnId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var now = DateTimeOffset.UtcNow;
        var addedTasks = new[]
        {
            new TaskItem(Guid.NewGuid(), DemoProjectId, columnId, $"{marker}-one", null,
                TaskPriority.High, DemoOwnerId, null, 2_048, now),
            new TaskItem(Guid.NewGuid(), DemoProjectId, columnId, $"{marker}-two", null,
                TaskPriority.High, DemoOwnerId, null, 2_048, now),
            new TaskItem(Guid.NewGuid(), DemoProjectId, columnId, $"{marker}-three", null,
                TaskPriority.High, DemoOwnerId, null, 3_072, now)
        };
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        dbContext.Tasks.AddRange(addedTasks);
        await dbContext.SaveChangesAsync();

        try
        {
            var repository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();
            var filter = new TaskFilter(Priority: TaskPriority.High, Search: marker);
            var board = await repository.GetSnapshotAsync(DemoProjectId, DemoOwnerId, filter, 2, default);
            Assert.NotNull(board);
            var backlog = board.Columns.Single(column => column.Id == columnId);
            var boardTaskIds = backlog.Tasks.Select(task => task.Id).ToArray();

            var firstPage = await repository.GetTaskPageAsync(
                DemoProjectId, columnId, DemoOwnerId, filter, 2, null, null, board.BoardVersion, default);
            Assert.NotNull(firstPage);
            Assert.NotNull(firstPage.Page);
            var cursor = firstPage.Page.Items[^1];
            var secondPage = await repository.GetTaskPageAsync(
                DemoProjectId, columnId, DemoOwnerId, filter, 2, cursor.Position, cursor.Id, board.BoardVersion, default);
            Assert.NotNull(secondPage);
            Assert.NotNull(secondPage.Page);
            var allPageIds = firstPage.Page.Items.Concat(secondPage.Page.Items).Select(task => task.Id).ToArray();

            Assert.Equal(3, backlog.TaskTotal);
            Assert.True(backlog.HasMoreTasks);
            Assert.Equal(2, boardTaskIds.Length);
            Assert.All(board.Columns.Where(column => column.Id != columnId), column =>
            {
                Assert.Empty(column.Tasks);
                Assert.Equal(0, column.TaskTotal);
                Assert.False(column.HasMoreTasks);
            });
            Assert.Equal(boardTaskIds, firstPage.Page.Items.Select(task => task.Id));
            Assert.Equal(3, firstPage.Page.Total);
            Assert.True(firstPage.Page.HasMore);
            Assert.Single(secondPage.Page.Items);
            Assert.Equal(3, secondPage.Page.Total);
            Assert.False(secondPage.Page.HasMore);
            Assert.Equal(board.BoardVersion, firstPage.Page.BoardVersion);
            Assert.Equal(3, allPageIds.Length);
            Assert.Equal(3, allPageIds.Distinct().Count());
            Assert.Equal(addedTasks.Select(task => task.Id).Order(), allPageIds.Order());
        }
        finally
        {
            await dbContext.Tasks.Where(task => addedTasks.Select(item => item.Id).Contains(task.Id)).ExecuteDeleteAsync();
        }
    }

    [DockerFact]
    public async Task BoardRepository_SnapshotCommandCountIsConstantAsColumnsGrow()
    {
        database.EnsureAvailable();
        var marker = $"snapshot-scale-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var columns = Enumerable.Range(1, 12)
            .Select(index => new BoardColumn(Guid.NewGuid(), DemoProjectId, $"Scale {index}", 10_000 + index * 1024, now))
            .ToArray();
        var tasks = columns.SelectMany(column => Enumerable.Range(1, 5)
            .Select(index => new TaskItem(Guid.NewGuid(), DemoProjectId, column.Id, $"{marker}-{index}", null,
                TaskPriority.Medium, DemoOwnerId, null, index * 1024, now)))
            .ToArray();
        var interceptor = new CountingCommandInterceptor();
        var options = new DbContextOptionsBuilder<ScrumBoardDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new ScrumBoardDbContext(options);
        var repository = new BoardRepository(dbContext);

        var baseline = await repository.GetSnapshotAsync(
            DemoProjectId, DemoOwnerId, new TaskFilter(Search: marker), 2, default);
        var baselineCommands = interceptor.ReaderCommandCount;
        dbContext.Columns.AddRange(columns);
        dbContext.Tasks.AddRange(tasks);
        await dbContext.SaveChangesAsync();
        var scaledStart = interceptor.ReaderCommandCount;

        try
        {
            var scaled = await repository.GetSnapshotAsync(
                DemoProjectId, DemoOwnerId, new TaskFilter(Search: marker), 2, default);

            Assert.NotNull(baseline);
            Assert.Equal(3, baselineCommands);
            Assert.NotNull(scaled);
            Assert.Equal(3, interceptor.ReaderCommandCount - scaledStart);
            var scaledColumns = scaled.Columns.Where(column => columns.Any(added => added.Id == column.Id)).ToArray();
            Assert.Equal(columns.Length, scaledColumns.Length);
            Assert.All(scaledColumns, column =>
            {
                Assert.Equal(5, column.TaskTotal);
                Assert.Equal(2, column.Tasks.Count);
                Assert.True(column.HasMoreTasks);
                Assert.Equal(column.Tasks.OrderBy(task => task.Position).ThenBy(task => task.Id), column.Tasks);
            });
        }
        finally
        {
            await dbContext.Tasks.Where(task => tasks.Select(item => item.Id).Contains(task.Id)).ExecuteDeleteAsync();
            await dbContext.Columns.Where(column => columns.Select(item => item.Id).Contains(column.Id)).ExecuteDeleteAsync();
        }
    }

    [DockerFact]
    public async Task BoardRepository_StaleTaskPageStopsAfterAuthorizedVersionQuery()
    {
        database.EnsureAvailable();
        var interceptor = new CountingCommandInterceptor();
        var options = new DbContextOptionsBuilder<ScrumBoardDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new ScrumBoardDbContext(options);
        var repository = new BoardRepository(dbContext);

        var exception = await Assert.ThrowsAsync<ScrumBoard.Application.Errors.OptimisticConcurrencyException>(() =>
            repository.GetTaskPageAsync(DemoProjectId,
                Guid.Parse("30000000-0000-0000-0000-000000000001"), DemoOwnerId,
                new TaskFilter(), 20, null, null, long.MaxValue, default));

        Assert.Equal("version_mismatch", exception.Code);
        Assert.Equal(1, interceptor.ReaderCommandCount);
    }

    [DockerFact]
    public async Task BoardRepository_AppendPositionUsesScalarMaxQuery()
    {
        database.EnsureAvailable();
        var interceptor = new CountingCommandInterceptor();
        var options = new DbContextOptionsBuilder<ScrumBoardDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new ScrumBoardDbContext(options);
        var repository = new BoardRepository(dbContext);

        var max = await repository.GetMaxTaskPositionAsync(DemoProjectId,
            Guid.Parse("30000000-0000-0000-0000-000000000001"), null, default);

        Assert.NotNull(max);
        Assert.Equal(1, interceptor.ReaderCommandCount);
        Assert.Contains("max(", interceptor.LastReaderCommandText, StringComparison.OrdinalIgnoreCase);
    }

    [DockerFact]
    public async Task Database_RejectsTaskWhoseColumnBelongsToAnotherProjectAndHasOrderingIndexes()
    {
        database.EnsureAvailable();
        var project = new Project(Guid.NewGuid(), "Integrity project", null, new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 20), ProjectStatus.Active, DemoOwnerId, DateTimeOffset.UtcNow);
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        var taskId = Guid.NewGuid();

        try
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tasks
                    (id, project_id, column_id, title, description, priority, assignee_id, due_date,
                     position, version, created_at, updated_at)
                VALUES
                    ({taskId}, {project.Id}, {Guid.Parse("30000000-0000-0000-0000-000000000001")},
                     'Cross-project task', NULL, 'Low', {DemoOwnerId}, NULL, 1024, 1, NOW(), NOW())
                """));
            var indexColumns = await dbContext.Database.SqlQueryRaw<string>(
                """
                SELECT index_class.relname || ':' || string_agg(attribute.attname, ',' ORDER BY key.ordinality) AS "Value"
                FROM pg_index AS index
                JOIN pg_class AS table_class ON table_class.oid = index.indrelid
                JOIN pg_class AS index_class ON index_class.oid = index.indexrelid
                CROSS JOIN LATERAL unnest(index.indkey) WITH ORDINALITY AS key(attnum, ordinality)
                JOIN pg_attribute AS attribute
                  ON attribute.attrelid = table_class.oid AND attribute.attnum = key.attnum
                WHERE index_class.relname IN ('ix_board_columns_project_position', 'ix_tasks_column_position')
                GROUP BY index_class.relname
                """).ToArrayAsync();

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
            Assert.Equal("FK_tasks_board_columns_project_id_column_id", exception.ConstraintName);
            Assert.Contains("ix_board_columns_project_position:project_id,position,id", indexColumns);
            Assert.Contains("ix_tasks_column_position:column_id,position,id", indexColumns);
        }
        finally
        {
            await dbContext.Projects.Where(item => item.Id == project.Id).ExecuteDeleteAsync();
        }
    }

    [DockerFact]
    public async Task ProjectRepository_DeletesProjectWithAssignedTasks()
    {
        database.EnsureAvailable();
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid(), "Delete project", null, new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 20), ProjectStatus.Active, DemoOwnerId, now);
        var column = new BoardColumn(Guid.NewGuid(), project.Id, "Backlog", 1024, now);
        var task = new TaskItem(Guid.NewGuid(), project.Id, column.Id, "Assigned task", null,
            TaskPriority.High, DemoOwnerId, null, 1024, now);
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        dbContext.AddRange(project, column, task);
        await dbContext.SaveChangesAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var loaded = await repository.FindAsync(project.Id, default);

        Assert.NotNull(loaded);
        await repository.RemoveAsync(loaded, default);
        await ((IUnitOfWork)dbContext).SaveChangesAsync(default);

        Assert.False(await dbContext.Projects.AnyAsync(item => item.Id == project.Id));
        Assert.False(await dbContext.Tasks.AnyAsync(item => item.Id == task.Id));
    }

    [DockerFact]
    public async Task UnitOfWork_TranslatesColumnDeleteForeignKeyRaceToConflict()
    {
        database.EnsureAvailable();
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid(), "Column race", null, new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 20), ProjectStatus.Active, DemoOwnerId, now);
        var column = new BoardColumn(Guid.NewGuid(), project.Id, "Backlog", 1024, now);
        await using var firstContext = new ScrumBoardDbContext(
            new DbContextOptionsBuilder<ScrumBoardDbContext>().UseNpgsql(database.ConnectionString).Options);
        firstContext.AddRange(project, column);
        await firstContext.SaveChangesAsync();

        try
        {
            var deletingColumn = await firstContext.Columns.SingleAsync(item => item.Id == column.Id);
            Assert.False(await firstContext.Tasks.AnyAsync(item => item.ColumnId == column.Id));
            firstContext.Columns.Remove(deletingColumn);

            await using var racingContext = new ScrumBoardDbContext(
                new DbContextOptionsBuilder<ScrumBoardDbContext>().UseNpgsql(database.ConnectionString).Options);
            racingContext.Tasks.Add(new TaskItem(Guid.NewGuid(), project.Id, column.Id, "Racing task", null,
                TaskPriority.Low, DemoOwnerId, null, 1024, now));
            await racingContext.SaveChangesAsync();

            var exception = await Assert.ThrowsAsync<ScrumBoard.Application.Errors.ConflictException>(() =>
                ((IUnitOfWork)firstContext).SaveChangesAsync(default));
            Assert.Equal("column_not_empty", exception.Code);
        }
        finally
        {
            await using var cleanup = new ScrumBoardDbContext(
                new DbContextOptionsBuilder<ScrumBoardDbContext>().UseNpgsql(database.ConnectionString).Options);
            await cleanup.Tasks.Where(item => item.ProjectId == project.Id).ExecuteDeleteAsync();
            await cleanup.Projects.Where(item => item.Id == project.Id).ExecuteDeleteAsync();
        }
    }

    [DockerFact]
    public async Task ReportDataSource_AuthorizesAndLoadsFilteredReportWithOneSqlCommand()
    {
        database.EnsureAvailable();
        var interceptor = new CountingCommandInterceptor();
        var options = new DbContextOptionsBuilder<ScrumBoardDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new ScrumBoardDbContext(options);
        var dataSource = new ReportDataSource(dbContext);
        using var cancellation = new CancellationTokenSource();

        var report = await dataSource.GetAsync(DemoProjectId, DemoOwnerId,
            new TaskFilter(Search: "i"),
            DateTimeOffset.UtcNow, ReportUseCase.MaximumSynchronousTaskRows + 1, cancellation.Token);

        Assert.NotNull(report);
        Assert.Equal("ScrumBoard Launch", report.ProjectName);
        Assert.Equal(
            ["Review product backlog", "Build collaborative board", "Define architecture"],
            report.Tasks.Select(task => task.Title));
        Assert.Equal(["Backlog", "In progress", "Done"], report.Tasks.Select(task => task.Column));
        Assert.Equal(1, interceptor.ReaderCommandCount);
        Assert.Contains("LIMIT", interceptor.LastReaderCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(interceptor.LastReaderParameterValues,
            value => value is int intValue && intValue == ReportUseCase.MaximumSynchronousTaskRows + 1);
        Assert.Equal(cancellation.Token, interceptor.LastReaderCancellationToken);
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

        var firstResponse = await client.PostAsJsonAsync("/api/v1/projects", request, ApiJsonOptions);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var replayResponse = await client.PostAsJsonAsync("/api/v1/projects", request, ApiJsonOptions);
        var replayBody = await replayResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        Assert.True(replayResponse.Headers.TryGetValues("Idempotency-Replayed", out var values));
        Assert.Contains("true", values);
        Assert.Equal(firstResponse.Headers.ETag, replayResponse.Headers.ETag);
        Assert.Equal(firstResponse.Headers.Location, replayResponse.Headers.Location);
        Assert.Equal(firstResponse.Content.Headers.ContentType, replayResponse.Content.Headers.ContentType);
        Assert.Equal(firstBody, replayBody);

        var conflictResponse = await client.PostAsJsonAsync(
            "/api/v1/projects", request with { Name = "Different" }, ApiJsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        var crossOperationResponse = await client.PostAsJsonAsync($"/api/v1/projects/{DemoProjectId}/tasks",
            new CreateTask(Guid.Parse("30000000-0000-0000-0000-000000000001"), "Different operation", null,
                TaskPriority.Low, DemoOwnerId, null), ApiJsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, crossOperationResponse.StatusCode);

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        var routeKey = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("Idempotency-Key", routeKey);
        var routeRequest = new CreateTask(
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            $"Route fingerprint {Guid.NewGuid():N}", null, TaskPriority.Low, DemoOwnerId, null);
        var firstRouteResponse = await client.PostAsJsonAsync(
            $"/api/v1/projects/{DemoProjectId}/tasks", routeRequest, ApiJsonOptions);
        var otherRouteResponse = await client.PostAsJsonAsync(
            $"/api/v1/projects/{Guid.NewGuid()}/tasks", routeRequest, ApiJsonOptions);

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

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/v1/projects/{DemoProjectId}/tasks", request, ApiJsonOptions);
        var replayResponse = await client.PostAsJsonAsync(
            $"/api/v1/projects/{DemoProjectId}/tasks", request, ApiJsonOptions);

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
    public async Task ProjectRepresentationEtagChangesWhenTouchBoardChangesReturnedRepresentation()
    {
        database.EnsureAvailable();
        var now = DateTimeOffset.UtcNow;
        var project = new Project(Guid.NewGuid(), "ETag project", null, new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 20), ProjectStatus.Active, DemoOwnerId, now);
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        try
        {
            var before = await repository.GetDetailsAsync(project.Id, DemoOwnerId, default);
            project.TouchBoard(now.AddMinutes(1));
            await ((IUnitOfWork)dbContext).SaveChangesAsync(default);
            var after = await repository.GetDetailsAsync(project.Id, DemoOwnerId, default);

            Assert.NotNull(before);
            Assert.NotNull(after);
            Assert.Equal(before.BoardVersion + 1, after.BoardVersion);
            Assert.Equal(before.Version + 1, after.Version);
            Assert.NotEqual(before.UpdatedAt, after.UpdatedAt);
            Assert.NotEqual(EntityTags.Format(before.Version), EntityTags.Format(after.Version));
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
            await dbContext.Projects.Where(item => item.Id == project.Id).ExecuteDeleteAsync();
        }
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

        var missingTag = await client.PutAsJsonAsync(
            $"/api/v1/projects/{DemoProjectId}", update, ApiJsonOptions);
        using var staleRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/projects/{DemoProjectId}")
        {
            Content = JsonContent.Create(update, options: ApiJsonOptions)
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

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

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

internal sealed class CountingCommandInterceptor : DbCommandInterceptor
{
    private int _readerCommandCount;

    public int ReaderCommandCount => _readerCommandCount;
    public string LastReaderCommandText { get; private set; } = string.Empty;
    public IReadOnlyList<object?> LastReaderParameterValues { get; private set; } = [];
    public CancellationToken LastReaderCancellationToken { get; private set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _readerCommandCount);
        LastReaderCommandText = command.CommandText;
        LastReaderParameterValues = command.Parameters.Cast<DbParameter>()
            .Select(parameter => parameter.Value)
            .ToArray();
        LastReaderCancellationToken = cancellationToken;
        return ValueTask.FromResult(result);
    }
}
