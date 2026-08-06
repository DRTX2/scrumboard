namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record TaskPageResponse(
    IReadOnlyList<BoardTaskResponse> Items,
    long Total,
    bool HasMore,
    string Etag);
