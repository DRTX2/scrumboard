namespace ScrumBoard.Adapters.Inbound.SignalR;

internal sealed record ColumnDeletedPayload(Guid ColumnId, bool Deleted, long BoardVersion);
