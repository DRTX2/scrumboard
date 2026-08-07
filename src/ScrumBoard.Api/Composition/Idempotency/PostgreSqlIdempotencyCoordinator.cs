using ScrumBoard.Adapters.Inbound.Infrastructure.Idempotency;
using ScrumBoard.Adapters.Outbound.Persistence.Idempotency;

namespace ScrumBoard.Api.Composition.Idempotency;

internal sealed class PostgreSqlIdempotencyCoordinator(
    PostgreSqlIdempotencyStore store) : IIdempotencyCoordinator
{
    public async Task<IdempotencyReservation> ReserveAsync(
        Guid userId,
        string operation,
        string key,
        string requestHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var reservation = await store.ReserveAsync(
            userId, operation, key, requestHash, createdAt, expiresAt, cancellationToken);
        return ToInbound(reservation);
    }

    public Task BeginExecutionAsync(CancellationToken cancellationToken) =>
        store.BeginExecutionAsync(cancellationToken);

    public async Task CompleteAndCommitAsync(
        Guid reservationId,
        IdempotentResponse response,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await store.CompleteAndCommitAsync(
            reservationId,
            new StoredIdempotentResponse(
                response.StatusCode,
                response.ContentType,
                response.ResponseBody,
                response.Location,
                response.Etag,
                response.BoardEtag),
            completedAt,
            cancellationToken);
    }

    public Task AbortAsync(Guid reservationId, CancellationToken cancellationToken) =>
        store.AbortAsync(reservationId, cancellationToken);

    private static IdempotencyReservation ToInbound(StoredIdempotencyReservation reservation) =>
        reservation.State is StoredIdempotencyState.Completed
        ? new IdempotencyReservation(
            reservation.Id,
            IdempotencyReservationState.Completed,
            reservation.RequestHash,
            new IdempotentResponse(
                reservation.Response!.StatusCode,
                reservation.Response.ContentType,
                reservation.Response.ResponseBody,
                reservation.Response.Location,
                reservation.Response.Etag,
                reservation.Response.BoardEtag))
        : new IdempotencyReservation(
            reservation.Id,
            reservation.State is StoredIdempotencyState.Acquired
                ? IdempotencyReservationState.Acquired
                : IdempotencyReservationState.InProgress,
            reservation.RequestHash,
            null);
}
