using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Boards;
using ScrumBoard.Application.Common;
using ScrumBoard.Application.Projects;
using ScrumBoard.Application.Sessions;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.UnitTests;

internal sealed class StubCurrentUser(Guid userId, bool isAuthenticated = true) : ICurrentUser
{
    public Guid UserId { get; } = userId;
    public bool IsAuthenticated { get; } = isAuthenticated;
}

internal sealed class StubClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

internal sealed class TrackingUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class TrackingNotifier : IBoardNotifier
{
    public List<(Guid ProjectId, string EventName, object Payload)> Published { get; } = [];

    public Task PublishAsync(Guid projectId, string eventName, object payload, CancellationToken cancellationToken)
    {
        Published.Add((projectId, eventName, payload));
        return Task.CompletedTask;
    }
}

internal sealed class FakeProjectRepository : IProjectRepository
{
    public Dictionary<Guid, Project> Projects { get; } = [];
    public ProjectListQuery? LastListQuery { get; private set; }
    public Guid? LastListUserId { get; private set; }
    public Project? Added { get; private set; }
    public Project? Removed { get; private set; }
    public Func<Guid, Guid, ProjectDetails?>? DetailsFactory { get; init; }

    public Task<PagedResult<ProjectSummary>> ListAsync(
        Guid userId,
        ProjectListQuery query,
        CancellationToken cancellationToken)
    {
        LastListUserId = userId;
        LastListQuery = query;
        return Task.FromResult(new PagedResult<ProjectSummary>([], query.Page, query.PageSize, 0));
    }

    public Task<Project?> FindAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.GetValueOrDefault(projectId));

    public Task<ProjectDetails?> GetDetailsAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(DetailsFactory?.Invoke(projectId, userId));

    public void Add(Project project)
    {
        Added = project;
        Projects[project.Id] = project;
    }

    public void Remove(Project project) => Removed = project;
}

internal sealed class FakeBoardRepository : IBoardRepository
{
    public List<BoardColumn> Columns { get; } = [];
    public List<TaskItem> Tasks { get; } = [];
    public BoardSnapshot? Snapshot { get; set; }
    public BoardFilter? LastFilter { get; private set; }
    public bool ColumnContainsTasks { get; set; }
    public BoardColumn? AddedColumn { get; private set; }
    public BoardColumn? RemovedColumn { get; private set; }
    public TaskItem? AddedTask { get; private set; }
    public TaskItem? RemovedTask { get; private set; }

    public Task<BoardSnapshot?> GetSnapshotAsync(
        Guid projectId,
        Guid userId,
        BoardFilter filter,
        CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult(Snapshot);
    }

    public Task<List<BoardColumn>> GetColumnsAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Columns.Where(column => column.ProjectId == projectId).OrderBy(column => column.Position).ToList());

    public Task<BoardColumn?> FindColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken) =>
        Task.FromResult(Columns.SingleOrDefault(column => column.ProjectId == projectId && column.Id == columnId));

    public Task<bool> ColumnContainsTasksAsync(Guid columnId, CancellationToken cancellationToken) =>
        Task.FromResult(ColumnContainsTasks);

    public Task<List<TaskItem>> GetTasksAsync(
        Guid projectId,
        Guid columnId,
        Guid? excludedTaskId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Tasks.Where(task => task.ProjectId == projectId && task.ColumnId == columnId && task.Id != excludedTaskId)
            .OrderBy(task => task.Position).ToList());

    public Task<TaskItem?> FindTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult(Tasks.SingleOrDefault(task => task.ProjectId == projectId && task.Id == taskId));

    public void AddColumn(BoardColumn column) => AddedColumn = column;
    public void RemoveColumn(BoardColumn column) => RemovedColumn = column;
    public void AddTask(TaskItem task) => AddedTask = task;
    public void RemoveTask(TaskItem task) => RemovedTask = task;
}

internal sealed class FakeUserRepository : IUserRepository
{
    public User? User { get; set; }
    public string? RequestedEmail { get; private set; }

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        RequestedEmail = normalizedEmail;
        return Task.FromResult(User);
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(User?.Id == id ? User : null);
}

internal sealed class StubPasswordHasher(bool result) : IPasswordHasher
{
    public bool Verify(string password, string encodedHash) => result;
}

internal sealed class TrackingTokenIssuer(SessionToken token) : ITokenIssuer
{
    public User? IssuedFor { get; private set; }

    public SessionToken Issue(User user)
    {
        IssuedFor = user;
        return token;
    }
}
