namespace ScrumBoard.Api.Adapters.SignalR;

internal sealed record PresenceSnapshot(IReadOnlyList<PresenceUser> Users, long Version);
