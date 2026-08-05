using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ScrumBoard.Api.Infrastructure.Idempotency;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Models;

namespace ScrumBoard.Api.Adapters.Outbound.Persistence;

internal sealed class PostgreSqlIdempotencyCoordinator(
    ScrumBoardDbContext dbContext,
    IServiceScopeFactory scopeFactory) : IIdempotencyCoordinator
{
    private IDbContextTransaction? _transaction;
    private bool _commitAttempted;
    private bool _committed;

    public async Task<IdempotencyReservation> ReserveAsync(
        Guid userId,
        string operation,
        string key,
        string requestHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await dbContext.IdempotencyRecords
            .Where(record => record.UserId == userId && record.Key == key && record.ExpiresAt <= createdAt)
            .ExecuteDeleteAsync(cancellationToken);

        var existing = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(record =>
            record.UserId == userId && record.Key == key,
            cancellationToken);
        if (existing is not null) return ToReservation(existing);

        var record = new IdempotencyRecordRow(
            Guid.NewGuid(), userId, operation, key, requestHash, createdAt, expiresAt);
        dbContext.IdempotencyRecords.Add(record);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new IdempotencyReservation(record.Id, IdempotencyReservationState.Acquired, requestHash, null);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_idempotency_user_key"
            })
        {
            dbContext.Entry(record).State = EntityState.Detached;
            var concurrent = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(item =>
                item.UserId == userId && item.Key == key,
                cancellationToken);
            if (concurrent is null) throw;
            return ToReservation(concurrent);
        }
    }

    public async Task BeginExecutionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null) throw new InvalidOperationException("An idempotent execution is already active.");
        _transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task CompleteAndCommitAsync(
        Guid reservationId,
        IdempotentResponse response,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        if (_transaction is null) throw new InvalidOperationException("No idempotent execution is active.");
        var record = dbContext.IdempotencyRecords.Local.SingleOrDefault(item => item.Id == reservationId)
            ?? await dbContext.IdempotencyRecords.SingleAsync(item => item.Id == reservationId, cancellationToken);
        record.Complete(response.StatusCode, response.ContentType, response.ResponseBody, response.Location,
            response.Etag, response.BoardEtag, completedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        _commitAttempted = true;
        await _transaction.CommitAsync(cancellationToken);
        _committed = true;
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task AbortAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        if (_committed)
        {
            dbContext.ChangeTracker.Clear();
            return;
        }

        if (_commitAttempted)
        {
            if (_transaction is not null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
            dbContext.ChangeTracker.Clear();
            await ReconcileAmbiguousCommitAsync(reservationId, cancellationToken);
            return;
        }

        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        dbContext.ChangeTracker.Clear();
        await dbContext.IdempotencyRecords
            .Where(record => record.Id == reservationId && record.CompletedAt == null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task ReconcileAmbiguousCommitAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var verificationContext = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
        var completed = await verificationContext.IdempotencyRecords.AsNoTracking()
            .Where(record => record.Id == reservationId)
            .Select(record => record.CompletedAt != null)
            .SingleOrDefaultAsync(cancellationToken);
        if (completed) return;
        await verificationContext.IdempotencyRecords
            .Where(record => record.Id == reservationId && record.CompletedAt == null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static IdempotencyReservation ToReservation(IdempotencyRecordRow record) => record.IsCompleted
        ? new IdempotencyReservation(
            record.Id,
            IdempotencyReservationState.Completed,
            record.RequestHash,
            new IdempotentResponse(
                record.StatusCode,
                record.ContentType ?? "application/json",
                record.ResponseBody ?? string.Empty,
                record.Location,
                record.Etag,
                record.BoardEtag))
        : new IdempotencyReservation(record.Id, IdempotencyReservationState.InProgress, record.RequestHash, null);
}
