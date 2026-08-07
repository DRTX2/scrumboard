using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Common;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Security;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Out;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.UnitTests.Application.UseCases;

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
    public List<BoardNotification> Published { get; } = [];

    public Task PublishAsync(BoardNotification notification, CancellationToken cancellationToken)
    {
        Published.Add(notification);
        return Task.CompletedTask;
    }
}

internal sealed class FakeProjectRepository : IProjectRepository
{
    public Dictionary<Guid, Project> Projects { get; } = [];
    public ProjectSearchCriteria? LastListCriteria { get; private set; }
    public Guid? LastListUserId { get; private set; }
    public Project? Added { get; private set; }
    public Project? Removed { get; private set; }
    public int FindCount { get; private set; }
    public Func<Guid, Guid, ProjectDetails?>? DetailsFactory { get; init; }

    public Task<PagedResult<ProjectSummary>> ListAsync(
        Guid userId,
        ProjectSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        LastListUserId = userId;
        LastListCriteria = criteria;
        return Task.FromResult(new PagedResult<ProjectSummary>([], criteria.Page, criteria.PageSize, 0));
    }

    public Task<Project?> FindAsync(Guid projectId, CancellationToken cancellationToken)
    {
        FindCount++;
        return Task.FromResult(Projects.GetValueOrDefault(projectId));
    }

    public Task<ProjectDetails?> GetDetailsAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(DetailsFactory?.Invoke(projectId, userId));

    public void Add(Project project)
    {
        Added = project;
        Projects[project.Id] = project;
    }

    public Task RemoveAsync(Project project, CancellationToken cancellationToken)
    {
        Removed = project;
        return Task.CompletedTask;
    }
}

internal sealed class FakeBoardRepository : IBoardRepository
{
    public List<BoardColumn> Columns { get; } = [];
    public List<TaskItem> Tasks { get; } = [];
    public BoardSnapshot? Snapshot { get; set; }
    public TaskPage? Page { get; set; }
    public TaskFilter? LastFilter { get; private set; }
    public int LastTaskLimit { get; private set; }
    public long? LastAfterPosition { get; private set; }
    public Guid? LastAfterTaskId { get; private set; }
    public bool ColumnContainsTasks { get; set; }
    public BoardColumn? AddedColumn { get; private set; }
    public BoardColumn? RemovedColumn { get; private set; }
    public TaskItem? AddedTask { get; private set; }
    public TaskItem? RemovedTask { get; private set; }
    public bool IsVisible { get; set; } = true;
    public int PageQueryCount { get; private set; }
    public int FullTaskQueryCount { get; private set; }
    public int MaxTaskPositionQueryCount { get; private set; }

    public Task<BoardSnapshot?> GetSnapshotAsync(
        Guid projectId,
        Guid userId,
        TaskFilter filter,
        int taskLimit,
        CancellationToken cancellationToken)
    {
        LastFilter = filter;
        LastTaskLimit = taskLimit;
        return Task.FromResult(IsVisible ? Snapshot : null);
    }

    public Task<TaskPageReadResult?> GetTaskPageAsync(
        Guid projectId,
        Guid columnId,
        Guid userId,
        TaskFilter filter,
        int limit,
        long? afterPosition,
        Guid? afterTaskId,
        long expectedBoardVersion,
        CancellationToken cancellationToken)
    {
        PageQueryCount++;
        if (!IsVisible) return Task.FromResult<TaskPageReadResult?>(null);
        LastFilter = filter;
        LastTaskLimit = limit;
        LastAfterPosition = afterPosition;
        LastAfterTaskId = afterTaskId;
        if (Page is not null && Page.BoardVersion != expectedBoardVersion)
        {
            throw new OptimisticConcurrencyException("version_mismatch", "El recurso cambió después de ser leído.");
        }

        return Task.FromResult<TaskPageReadResult?>(new TaskPageReadResult(Page));
    }

    public Task<List<BoardMember>?> GetMembersAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<List<BoardMember>?>(IsVisible ? Snapshot?.Members.ToList() ?? [] : null);

    public Task<List<BoardColumn>> GetColumnsAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Columns.Where(column => column.ProjectId == projectId).OrderBy(column => column.Position).ToList());

    public Task<BoardColumn?> FindColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken) =>
        Task.FromResult(Columns.SingleOrDefault(column => column.ProjectId == projectId && column.Id == columnId));

    public Task<bool> ColumnContainsTasksAsync(Guid columnId, CancellationToken cancellationToken) =>
        Task.FromResult(ColumnContainsTasks);

    public Task<long?> GetMaxTaskPositionAsync(
        Guid projectId,
        Guid columnId,
        Guid? excludedTaskId,
        CancellationToken cancellationToken)
    {
        MaxTaskPositionQueryCount++;
        return Task.FromResult(Tasks
            .Where(task => task.ProjectId == projectId && task.ColumnId == columnId && task.Id != excludedTaskId)
            .Select(task => (long?)task.Position)
            .Max());
    }

    public Task<TaskOrderNeighbors?> GetTaskOrderNeighborsAsync(
        Guid projectId,
        Guid columnId,
        Guid excludedTaskId,
        Guid? beforeTaskId,
        Guid? afterTaskId,
        CancellationToken cancellationToken)
    {
        var siblings = Tasks
            .Where(task => task.ProjectId == projectId && task.ColumnId == columnId && task.Id != excludedTaskId)
            .OrderBy(task => task.Position).ThenBy(task => task.Id).ToList();
        var beforeIndex = beforeTaskId is null ? -1 : siblings.FindIndex(task => task.Id == beforeTaskId);
        var afterIndex = afterTaskId is null ? -1 : siblings.FindIndex(task => task.Id == afterTaskId);
        if (beforeTaskId is not null && beforeIndex < 0 || afterTaskId is not null && afterIndex < 0 ||
            beforeTaskId is not null && afterTaskId is not null && afterIndex + 1 != beforeIndex)
        {
            return Task.FromResult<TaskOrderNeighbors?>(null);
        }

        long? previous = afterIndex >= 0 ? siblings[afterIndex].Position : beforeIndex > 0 ? siblings[beforeIndex - 1].Position : null;
        long? next = beforeIndex >= 0 ? siblings[beforeIndex].Position : afterIndex >= 0 && afterIndex + 1 < siblings.Count
            ? siblings[afterIndex + 1].Position
            : null;
        return Task.FromResult<TaskOrderNeighbors?>(new TaskOrderNeighbors(previous, next));
    }

    public Task<List<TaskItem>> GetTasksAsync(
        Guid projectId,
        Guid columnId,
        Guid? excludedTaskId,
        CancellationToken cancellationToken)
    {
        FullTaskQueryCount++;
        return Task.FromResult(Tasks.Where(task => task.ProjectId == projectId && task.ColumnId == columnId && task.Id != excludedTaskId)
            .OrderBy(task => task.Position).ThenBy(task => task.Id).ToList());
    }

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

internal sealed class TrackingPasswordHasher(bool result, string dummyHash = "dummy-hash") : IPasswordHasher
{
    public string DummyHash { get; } = dummyHash;
    public List<(string Password, string EncodedHash)> Verifications { get; } = [];

    public bool Verify(string password, string encodedHash)
    {
        Verifications.Add((password, encodedHash));
        return result;
    }
}

internal sealed class TrackingTokenIssuer(IssuedToken token) : ITokenIssuer
{
    public User? IssuedFor { get; private set; }

    public IssuedToken Issue(User user)
    {
        IssuedFor = user;
        return token;
    }
}

internal sealed class FakeReportDataSource : IReportDataSource
{
    public ProjectReportData? Result { get; set; }
    public TaskFilter? LastFilter { get; private set; }
    public Guid? LastUserId { get; private set; }
    public int? LastTaskRowLimit { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }
    public int CallCount { get; private set; }

    public Task<ProjectReportData?> GetAsync(
        Guid projectId,
        Guid userId,
        TaskFilter filter,
        DateTimeOffset generatedAt,
        int taskRowLimit,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastUserId = userId;
        LastFilter = filter;
        LastTaskRowLimit = taskRowLimit;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Result);
    }
}

internal sealed class CountOnlyReadOnlyList<T>(int count) : IReadOnlyList<T>
{
    public int Count { get; } = count;
    public T this[int index] => throw new NotSupportedException();

    public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class StubReportExporter(
    string format,
    string mediaType = "application/octet-stream",
    string fileExtension = "bin",
    byte[]? content = null) : IReportExporter
{
    public string Format { get; } = format;
    public string MediaType { get; } = mediaType;
    public string FileExtension { get; } = fileExtension;
    public ProjectReportData? Exported { get; private set; }

    public byte[] Export(ProjectReportData data)
    {
        Exported = data;
        return content ?? [1, 2, 3];
    }
}
