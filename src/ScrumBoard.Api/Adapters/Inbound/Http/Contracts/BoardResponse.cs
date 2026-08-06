namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record BoardResponse(
    BoardProjectResponse Project,
    IReadOnlyList<BoardColumnResponse> Columns,
    IReadOnlyList<UserResponse> Members,
    string Etag);
