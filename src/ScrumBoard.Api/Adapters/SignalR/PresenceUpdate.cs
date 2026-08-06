namespace ScrumBoard.Api.Adapters.SignalR;

internal sealed record PresenceUpdate(Guid ProjectId, PresenceSnapshot Snapshot);
