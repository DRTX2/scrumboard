namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record BoardProjectResponse(Guid Id, string Name, string Etag);
