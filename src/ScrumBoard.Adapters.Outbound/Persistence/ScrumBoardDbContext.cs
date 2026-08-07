using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScrumBoard.Adapters.Outbound.Persistence.Models;
using ScrumBoard.Adapters.Outbound.Persistence.Seed;
using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Ports.Out;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.Adapters.Outbound.Persistence;

public sealed class ScrumBoardDbContext(DbContextOptions<ScrumBoardDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<BoardColumn> Columns => Set<BoardColumn>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<IdempotencyRecordRow> IdempotencyRecords => Set<IdempotencyRecordRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScrumBoardDbContext).Assembly);
        DemoUserSeed.Configure(modelBuilder);
        DemoWorkspaceSeed.Configure(modelBuilder);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new OptimisticConcurrencyException("concurrent_update", "El recurso cambió durante la operación.", exception);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation,
                ConstraintName: { } constraintName
            } && constraintName.StartsWith("FK_tasks_board_columns", StringComparison.Ordinal) &&
            ChangeTracker.Entries<BoardColumn>().Any(entry => entry.State is EntityState.Deleted))
        {
            throw new ConflictException("column_not_empty", "No se puede eliminar una columna que contiene tareas.", exception);
        }
    }
}
