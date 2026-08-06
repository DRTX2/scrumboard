namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record ColumnMutationResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    long Position,
    string Etag,
    string BoardEtag);
