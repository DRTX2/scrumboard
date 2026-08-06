namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record BoardColumnResponse(
    Guid Id,
    string Name,
    long Position,
    string Etag,
    IReadOnlyList<BoardTaskResponse> Tasks,
    long TaskTotal,
    bool HasMoreTasks);
