namespace ScrumBoard.Adapters.Inbound.Infrastructure.Idempotency;

public enum IdempotencyReservationState
{
    Acquired,
    InProgress,
    Completed
}

public sealed record IdempotentResponse(
    int StatusCode,
    string ContentType,
    string ResponseBody,
    string? Location,
    string? Etag,
    string? BoardEtag);

public sealed record IdempotencyReservation(
    Guid Id,
    IdempotencyReservationState State,
    string RequestHash,
    IdempotentResponse? Response);

public interface IIdempotencyCoordinator
{
    Task<IdempotencyReservation> ReserveAsync(
        Guid userId,
        string operation,
        string key,
        string requestHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task BeginExecutionAsync(CancellationToken cancellationToken);
    Task CompleteAndCommitAsync(
        Guid reservationId,
        IdempotentResponse response,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);
    Task AbortAsync(Guid reservationId, CancellationToken cancellationToken);
}
