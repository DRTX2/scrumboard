using ScrumBoard.Application.Boards;
using ScrumBoard.Application.Common;
using ScrumBoard.Application.Projects;
using ScrumBoard.Application.Sessions;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Ordering;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.UnitTests;

public sealed class ApplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProjectList_NormalizesPagingSearchAndDirectionBeforeRepositoryCall()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeProjectRepository();
        var service = CreateProjectService(repository, userId);

        await service.ListAsync(new ProjectListQuery(0, 500, "  roadmap  ", "name", "unexpected"), default);

        Assert.Equal(userId, repository.LastListUserId);
        Assert.Equal(new ProjectListQuery(1, 100, "roadmap", "name", "desc"), repository.LastListQuery);
    }

    [Fact]
    public async Task ProjectList_WhenAnonymous_RejectsBeforeRepositoryCall()
    {
        var repository = new FakeProjectRepository();
        var service = new ProjectService(repository, new StubCurrentUser(Guid.Empty, false), new StubClock(Now),
            new TrackingUnitOfWork());

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.ListAsync(new ProjectListQuery(), default));

        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Null(repository.LastListQuery);
    }

    [Fact]
    public async Task ProjectUpdate_WithStaleVersion_DoesNotMutateOrSave()
    {
        var userId = Guid.NewGuid();
        var project = CreateProject(userId);
        var repository = new FakeProjectRepository();
        repository.Projects.Add(project.Id, project);
        var unitOfWork = new TrackingUnitOfWork();
        var service = new ProjectService(repository, new StubCurrentUser(userId), new StubClock(Now.AddHours(1)), unitOfWork);
        var request = new UpdateProject("Changed", null, project.StartDate, project.ExpectedEndDate, ProjectStatus.Completed);

        var exception = await Assert.ThrowsAsync<PreconditionFailedException>(() =>
            service.UpdateAsync(project.Id, request, expectedVersion: 99, default));

        Assert.Equal("etag_mismatch", exception.Code);
        Assert.Equal("Project", project.Name);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProjectDelete_ByMember_IsForbiddenAndDoesNotRemoveProject()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        project.AddMember(memberId, ProjectRole.Member, Now.AddMinutes(1));
        var repository = new FakeProjectRepository();
        repository.Projects.Add(project.Id, project);
        var unitOfWork = new TrackingUnitOfWork();
        var service = new ProjectService(repository, new StubCurrentUser(memberId), new StubClock(Now), unitOfWork);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DeleteAsync(project.Id, project.Version, default));

        Assert.Equal("project_owner_required", exception.Code);
        Assert.Null(repository.Removed);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task BoardGet_TrimsSearchBeforeQueryingRepository()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var projects = RepositoryContaining(project);
        var boards = new FakeBoardRepository
        {
            Snapshot = new BoardSnapshot(project.Id, project.Name, 1, [], [])
        };
        var service = CreateBoardService(projects, boards, ownerId);

        var result = await service.GetAsync(project.Id, new BoardFilter(Search: "  urgent  "), default);

        Assert.Same(boards.Snapshot, result);
        Assert.Equal("urgent", boards.LastFilter?.Search);
    }

    [Fact]
    public async Task CreateColumn_AppendsAfterExistingColumnsAndPublishesSavedVersion()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var projects = RepositoryContaining(project);
        var boards = new FakeBoardRepository();
        boards.Columns.Add(new BoardColumn(Guid.NewGuid(), project.Id, "Backlog", 1024, Now));
        var unitOfWork = new TrackingUnitOfWork();
        var notifier = new TrackingNotifier();
        var service = CreateBoardService(projects, boards, ownerId, unitOfWork, notifier);

        var response = await service.CreateColumnAsync(project.Id, new CreateColumn("Ready"), default);

        Assert.Equal(2048, response.Position);
        Assert.Equal(2, response.BoardVersion);
        Assert.NotNull(boards.AddedColumn);
        Assert.Equal(response.Id, boards.AddedColumn.Id);
        Assert.Equal("Ready", boards.AddedColumn.Name);
        Assert.Equal(1, unitOfWork.SaveCount);
        var published = Assert.Single(notifier.Published);
        Assert.Equal("ColumnChanged", published.EventName);
        Assert.Same(response, published.Payload);
    }

    [Fact]
    public async Task UpdateColumn_WithStaleVersion_DoesNotSaveOrPublish()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var projects = RepositoryContaining(project);
        var boards = new FakeBoardRepository();
        var column = new BoardColumn(Guid.NewGuid(), project.Id, "Backlog", 1024, Now);
        boards.Columns.Add(column);
        var unitOfWork = new TrackingUnitOfWork();
        var notifier = new TrackingNotifier();
        var service = CreateBoardService(projects, boards, ownerId, unitOfWork, notifier);

        var exception = await Assert.ThrowsAsync<PreconditionFailedException>(() =>
            service.UpdateColumnAsync(project.Id, column.Id, new UpdateColumn("Changed"), 2, default));

        Assert.Equal("etag_mismatch", exception.Code);
        Assert.Equal("Backlog", column.Name);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(notifier.Published);
    }

    [Fact]
    public async Task CreateTask_WithExternalAssignee_IsRejectedBeforePersistence()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var projects = RepositoryContaining(project);
        var boards = new FakeBoardRepository();
        var column = new BoardColumn(Guid.NewGuid(), project.Id, "Backlog", 1024, Now);
        boards.Columns.Add(column);
        var unitOfWork = new TrackingUnitOfWork();
        var service = CreateBoardService(projects, boards, ownerId, unitOfWork);
        var request = new CreateTask(column.Id, "Task", null, TaskPriority.Medium, Guid.NewGuid(), null);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateTaskAsync(project.Id, request, default));

        Assert.Equal("assignee_not_member", exception.Code);
        Assert.Null(boards.AddedTask);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task MoveTask_BetweenNeighbors_AssignsMidpointAndPublishesMove()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var projects = RepositoryContaining(project);
        var boards = new FakeBoardRepository();
        var source = new BoardColumn(Guid.NewGuid(), project.Id, "Source", 1024, Now);
        var target = new BoardColumn(Guid.NewGuid(), project.Id, "Target", 2048, Now);
        boards.Columns.AddRange([source, target]);
        var moved = CreateTask(project.Id, source.Id, 1024);
        var before = CreateTask(project.Id, target.Id, 1024);
        var after = CreateTask(project.Id, target.Id, 2048);
        boards.Tasks.AddRange([moved, before, after]);
        var notifier = new TrackingNotifier();
        var service = CreateBoardService(projects, boards, ownerId, notifier: notifier);

        var response = await service.MoveTaskAsync(project.Id, moved.Id,
            new MoveTask(target.Id, BeforeTaskId: after.Id, AfterTaskId: null), moved.Version, default);

        Assert.Equal(target.Id, moved.ColumnId);
        Assert.Equal(1536, response.Position);
        Assert.Equal(2, response.Version);
        Assert.Equal(2, response.BoardVersion);
        Assert.Equal("TaskMoved", Assert.Single(notifier.Published).EventName);
    }

    [Fact]
    public async Task MoveTask_WhenNeighborsAreAdjacent_RebalancesSiblingsBeforeInsert()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var projects = RepositoryContaining(project);
        var boards = new FakeBoardRepository();
        var column = new BoardColumn(Guid.NewGuid(), project.Id, "Backlog", 1024, Now);
        boards.Columns.Add(column);
        var moved = CreateTask(project.Id, column.Id, 3000);
        var first = CreateTask(project.Id, column.Id, 10);
        var second = CreateTask(project.Id, column.Id, 11);
        boards.Tasks.AddRange([moved, first, second]);
        var service = CreateBoardService(projects, boards, ownerId);

        var response = await service.MoveTaskAsync(project.Id, moved.Id,
            new MoveTask(column.Id, BeforeTaskId: second.Id, AfterTaskId: null), moved.Version, default);

        Assert.Equal(OrderPosition.Step, first.Position);
        Assert.Equal(OrderPosition.Step * 2, second.Position);
        Assert.Equal(1536, response.Position);
        Assert.Equal(2, first.Version);
        Assert.Equal(2, second.Version);
    }

    [Fact]
    public async Task SessionCreate_NormalizesEmailAndIssuesTokenForVerifiedUser()
    {
        var user = new User(Guid.NewGuid(), "Ada", "ada@example.com", "encoded", Now);
        var users = new FakeUserRepository { User = user };
        var token = new SessionToken("access-token", Now.AddMinutes(30));
        var issuer = new TrackingTokenIssuer(token);
        var service = new SessionService(users, new StubPasswordHasher(true), issuer);

        var response = await service.CreateAsync(new CreateSession("  ADA@EXAMPLE.COM  ", "password"), default);

        Assert.Equal("ada@example.com", users.RequestedEmail);
        Assert.Same(user, issuer.IssuedFor);
        Assert.Equal(token.AccessToken, response.AccessToken);
        Assert.Equal(token.ExpiresAt, response.ExpiresAt);
    }

    [Fact]
    public async Task SessionCreate_WithInvalidPassword_DoesNotIssueToken()
    {
        var user = new User(Guid.NewGuid(), "Ada", "ada@example.com", "encoded", Now);
        var users = new FakeUserRepository { User = user };
        var issuer = new TrackingTokenIssuer(new SessionToken("unused", Now));
        var service = new SessionService(users, new StubPasswordHasher(false), issuer);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.CreateAsync(new CreateSession(user.Email, "wrong"), default));

        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Null(issuer.IssuedFor);
    }

    private static ProjectService CreateProjectService(FakeProjectRepository repository, Guid userId) =>
        new(repository, new StubCurrentUser(userId), new StubClock(Now), new TrackingUnitOfWork());

    private static BoardService CreateBoardService(
        FakeProjectRepository projects,
        FakeBoardRepository boards,
        Guid userId,
        TrackingUnitOfWork? unitOfWork = null,
        TrackingNotifier? notifier = null) =>
        new(projects, boards, new StubCurrentUser(userId), new StubClock(Now.AddHours(1)),
            unitOfWork ?? new TrackingUnitOfWork(), notifier ?? new TrackingNotifier());

    private static FakeProjectRepository RepositoryContaining(Project project)
    {
        var repository = new FakeProjectRepository();
        repository.Projects.Add(project.Id, project);
        return repository;
    }

    private static Project CreateProject(Guid ownerId) =>
        new(Guid.NewGuid(), "Project", null, new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1),
            ProjectStatus.Active, ownerId, Now);

    private static TaskItem CreateTask(Guid projectId, Guid columnId, long position) =>
        new(Guid.NewGuid(), projectId, columnId, "Task", null, TaskPriority.Medium, null, null, position, Now);
}
