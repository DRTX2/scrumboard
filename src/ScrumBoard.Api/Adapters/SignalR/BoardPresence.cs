using System.Collections.Concurrent;

namespace ScrumBoard.Api.Adapters.SignalR;

public sealed class BoardPresence
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, PresenceUser>> _connections = new();
    private readonly ConcurrentDictionary<Guid, long> _versions = new();

    internal PresenceSnapshot Join(Guid projectId, string connectionId, PresenceUser user)
    {
        lock (_gate)
        {
            var projects = _connections.GetOrAdd(connectionId,
                static _ => new ConcurrentDictionary<Guid, PresenceUser>());
            projects[projectId] = user;
            return Snapshot(projectId);
        }
    }

    internal PresenceSnapshot? Leave(Guid projectId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connectionId, out var projects) ||
                !projects.TryRemove(projectId, out _)) return null;
            return Snapshot(projectId);
        }
    }

    internal IReadOnlyList<PresenceUpdate> LeaveConnection(string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryRemove(connectionId, out var projects)) return [];
            return projects.Keys.Select(projectId => new PresenceUpdate(projectId, Snapshot(projectId))).ToList();
        }
    }

    private PresenceSnapshot Snapshot(Guid projectId) =>
        new(UsersFor(projectId), _versions.AddOrUpdate(projectId, 1, static (_, version) => version + 1));

    private List<PresenceUser> UsersFor(Guid projectId) =>
        _connections.Values
            .Select(projects => projects.GetValueOrDefault(projectId))
            .OfType<PresenceUser>()
            .DistinctBy(user => user.Id)
            .OrderBy(user => user.Name)
            .ToList();
}
