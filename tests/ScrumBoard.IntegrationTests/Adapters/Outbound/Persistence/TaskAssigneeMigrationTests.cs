using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ScrumBoard.Adapters.Outbound.Persistence;

namespace ScrumBoard.IntegrationTests.Adapters.Outbound.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class TaskAssigneeMigrationTests(PostgreSqlFixture database)
{
    private static readonly Guid DemoOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DemoProjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid BacklogColumnId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [DockerFact]
    public async Task Migration_BackfillsNullAssigneeToOwnerBeforeMakingColumnRequired()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        var taskId = Guid.NewGuid();

        await migrator.MigrateAsync("20260805042120_HardenIdempotencyRecords");
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tasks
                    (id, project_id, column_id, title, description, priority, assignee_id, due_date,
                     position, version, created_at, updated_at)
                VALUES
                    ({taskId}, {DemoProjectId}, {BacklogColumnId}, 'Legacy unassigned task', NULL,
                     'Low', NULL, NULL, 8192, 1, NOW(), NOW())
                """);

            await migrator.MigrateAsync("20260806021724_RequireTaskAssigneeAndAddChecks");

            var assigneeId = await dbContext.Database.SqlQueryRaw<Guid>(
                "SELECT assignee_id AS \"Value\" FROM tasks WHERE id = {0}", taskId).SingleAsync();
            var nullable = await dbContext.Database.SqlQueryRaw<string>(
                "SELECT is_nullable AS \"Value\" FROM information_schema.columns " +
                "WHERE table_name = 'tasks' AND column_name = 'assignee_id'").SingleAsync();

            Assert.Equal(DemoOwnerId, assigneeId);
            Assert.Equal("NO", nullable);
        }
        finally
        {
            await migrator.MigrateAsync("20260806021724_RequireTaskAssigneeAndAddChecks");
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM tasks WHERE id = {taskId}");
        }
    }

    [DockerFact]
    public async Task Migration_ReassignsLegacyNonMemberAssigneeToDeterministicOwner()
    {
        database.EnsureAvailable();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        var migrator = dbContext.Database.GetService<IMigrator>();
        var taskId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var outsiderEmail = $"{outsiderId:N}@example.test";

        await migrator.MigrateAsync("20260805042120_HardenIdempotencyRecords");
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO users (id, name, email, password_hash, is_active, created_at)
                VALUES ({outsiderId}, 'Legacy outsider', {outsiderEmail}, 'legacy-hash', TRUE, NOW());

                INSERT INTO tasks
                    (id, project_id, column_id, title, description, priority, assignee_id, due_date,
                     position, version, created_at, updated_at)
                VALUES
                    ({taskId}, {DemoProjectId}, {BacklogColumnId}, 'Legacy external assignment', NULL,
                     'Low', {outsiderId}, NULL, 9216, 1, NOW(), NOW());
                """);

            await migrator.MigrateAsync("20260806021724_RequireTaskAssigneeAndAddChecks");

            var assigneeId = await dbContext.Database.SqlQueryRaw<Guid>(
                "SELECT assignee_id AS \"Value\" FROM tasks WHERE id = {0}", taskId).SingleAsync();

            Assert.Equal(DemoOwnerId, assigneeId);
        }
        finally
        {
            await migrator.MigrateAsync("20260806021724_RequireTaskAssigneeAndAddChecks");
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM tasks WHERE id = {taskId}");
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM users WHERE id = {outsiderId}");
        }
    }
}
