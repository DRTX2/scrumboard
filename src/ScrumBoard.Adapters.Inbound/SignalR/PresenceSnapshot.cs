namespace ScrumBoard.Adapters.Inbound.SignalR;

internal sealed record PresenceSnapshot(IReadOnlyList<PresenceUser> Users, long Version);
