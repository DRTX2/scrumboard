namespace ScrumBoard.Api.Adapters.SignalR;

internal sealed record ColumnDeletedPayload(Guid ColumnId, bool Deleted, long BoardVersion);
