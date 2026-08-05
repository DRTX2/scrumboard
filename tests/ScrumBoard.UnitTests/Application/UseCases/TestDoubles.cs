using ScrumBoard.Application.Context;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Common;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Security;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Outbound;
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
    public int MembershipCheckCount { get; private set; }
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

    public Task<Project?> FindAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.GetValueOrDefault(projectId));

    public Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        MembershipCheckCount++;
        return Task.FromResult(Projects.GetValueOrDefault(projectId)?.Members.Any(member => member.UserId == userId) is true);
    }

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
    public TaskFilter? LastFilter { get; private set; }
    public bool ColumnContainsTasks { get; set; }
    public BoardColumn? AddedColumn { get; private set; }
    public BoardColumn? RemovedColumn { get; private set; }
    public TaskItem? AddedTask { get; private set; }
    public TaskItem? RemovedTask { get; private set; }

    public Task<BoardSnapshot?> GetSnapshotAsync(
        Guid projectId,
        Guid userId,
        TaskFilter filter,
        CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult(Snapshot);
    }

    public Task<List<BoardMember>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot?.Members.ToList() ?? []);

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
    public int CallCount { get; private set; }

    public Task<ProjectReportData?> GetAsync(
        Guid projectId,
        TaskFilter filter,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastFilter = filter;
        return Task.FromResult(Result);
    }
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
