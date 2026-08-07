namespace ScrumBoard.Adapters.Inbound.SignalR;

internal sealed record PresenceUpdate(Guid ProjectId, PresenceSnapshot Snapshot);
