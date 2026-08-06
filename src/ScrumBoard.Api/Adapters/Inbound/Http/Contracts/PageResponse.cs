namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record PageResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long Total,
    int TotalPages);
