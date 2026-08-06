namespace ScrumBoard.Api.Adapters.SignalR;

internal sealed record PresenceChangedPayload(IReadOnlyList<PresenceUser> Users, int Count, long Version);
