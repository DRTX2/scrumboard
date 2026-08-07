namespace ScrumBoard.Adapters.Inbound.SignalR;

internal sealed record PresenceChangedPayload(IReadOnlyList<PresenceUser> Users, int Count, long Version);
