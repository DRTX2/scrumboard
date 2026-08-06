namespace ScrumBoard.Api.Adapters.SignalR;

internal sealed record TaskDeletedPayload(Guid TaskId, long BoardVersion);
