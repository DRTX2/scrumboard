using ScrumBoard.Api.Adapters.SignalR;

namespace ScrumBoard.UnitTests.Adapters.SignalR;

public sealed class BoardPresenceTests
{
    [Fact]
    public void Join_DeduplicatesTheSameUserAcrossConnections()
    {
        var presence = new BoardPresence();
        var projectId = Guid.NewGuid();
        var user = new PresenceUser(Guid.NewGuid(), "Ada");

        var first = presence.Join(projectId, "connection-1", user);
        var second = presence.Join(projectId, "connection-2", user);

        Assert.Equal(user, Assert.Single(second.Users));
        Assert.True(second.Version > first.Version);
    }

    [Fact]
    public void Leave_RemovesOnlyTheRequestedProjectSubscription()
    {
        var presence = new BoardPresence();
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        var user = new PresenceUser(Guid.NewGuid(), "Ada");
        presence.Join(firstProjectId, "connection", user);
        presence.Join(secondProjectId, "connection", user);

        var firstProjectUsers = presence.Leave(firstProjectId, "connection");
        var updates = presence.LeaveConnection("connection");

        Assert.NotNull(firstProjectUsers);
        Assert.Empty(firstProjectUsers.Users);
        var update = Assert.Single(updates);
        Assert.Equal(secondProjectId, update.ProjectId);
        Assert.Empty(update.Snapshot.Users);
    }

    [Fact]
    public void LeaveConnection_CleansEveryProjectAndPreservesOtherConnections()
    {
        var presence = new BoardPresence();
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        var firstUser = new PresenceUser(Guid.NewGuid(), "Ada");
        var secondUser = new PresenceUser(Guid.NewGuid(), "Grace");
        presence.Join(firstProjectId, "connection-1", firstUser);
        presence.Join(secondProjectId, "connection-1", firstUser);
        presence.Join(firstProjectId, "connection-2", secondUser);

        var updates = presence.LeaveConnection("connection-1").ToDictionary(update => update.ProjectId);

        Assert.Equal(secondUser, Assert.Single(updates[firstProjectId].Snapshot.Users));
        Assert.Empty(updates[secondProjectId].Snapshot.Users);
        Assert.Empty(presence.LeaveConnection("connection-1"));
    }

    [Fact]
    public void Leave_WhenConnectionWasNotSubscribed_DoesNotCreateAnUpdate()
    {
        var presence = new BoardPresence();

        var snapshot = presence.Leave(Guid.NewGuid(), "unknown-connection");

        Assert.Null(snapshot);
    }
}
