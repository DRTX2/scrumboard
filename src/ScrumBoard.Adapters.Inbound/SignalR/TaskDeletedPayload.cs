namespace ScrumBoard.Adapters.Inbound.SignalR;

internal sealed record TaskDeletedPayload(Guid TaskId, long BoardVersion);
