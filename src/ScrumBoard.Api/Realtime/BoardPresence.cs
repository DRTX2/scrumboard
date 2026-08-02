namespace ScrumBoard.Api.Realtime;

internal sealed record PresenceUser(Guid Id, string Name);

internal sealed class BoardPresence
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Dictionary<string, PresenceUser>> _connections = [];

    public IReadOnlyList<PresenceUser> Join(Guid projectId, string connectionId, PresenceUser user)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(projectId, out var connections))
            {
                connections = [];
                _connections[projectId] = connections;
            }
            connections[connectionId] = user;
            return DistinctUsers(connections);
        }
    }

    public IReadOnlyList<PresenceUser> Leave(Guid projectId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(projectId, out var connections)) return [];
            connections.Remove(connectionId);
            var users = DistinctUsers(connections);
            if (connections.Count == 0) _connections.Remove(projectId);
            return users;
        }
    }

    private static List<PresenceUser> DistinctUsers(Dictionary<string, PresenceUser> connections) =>
        connections.Values.DistinctBy(user => user.Id).OrderBy(user => user.Name).ToList();
}
