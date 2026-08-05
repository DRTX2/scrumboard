using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Security;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Inbound.Projects;
using ScrumBoard.Application.Ports.Inbound.Sessions;
using ScrumBoard.Application.UseCases.Boards;
using ScrumBoard.Application.UseCases.Projects;
using ScrumBoard.Application.UseCases.Reports;
using ScrumBoard.Application.UseCases.Sessions;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Ordering;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.UnitTests.Application.UseCases;

public sealed class ApplicationUseCaseTests
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
        Assert.Equal(new ProjectSearchCriteria(1, 100, "roadmap", ProjectSortField.Name, SortDirection.Descending),
            repository.LastListCriteria);
    }

    [Fact]
    public async Task ProjectList_WhenAnonymous_RejectsBeforeRepositoryCall()
    {
        var repository = new FakeProjectRepository();
        var service = new ProjectUseCase(repository, new StubCurrentUser(Guid.Empty, false), new StubClock(Now),
            new TrackingUnitOfWork());

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.ListAsync(new ProjectListQuery(), default));

        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Null(repository.LastListCriteria);
    }

    [Fact]
    public async Task ProjectUpdate_WithStaleVersion_DoesNotMutateOrSave()
    {
        var userId = Guid.NewGuid();
        var project = CreateProject(userId);
        var repository = new FakeProjectRepository();
        repository.Projects.Add(project.Id, project);
        var unitOfWork = new TrackingUnitOfWork();
        var service = new ProjectUseCase(repository, new StubCurrentUser(userId), new StubClock(Now.AddHours(1)), unitOfWork);
        var request = new UpdateProject("Changed", null, project.StartDate, project.ExpectedEndDate, ProjectStatus.Completed);

        var exception = await Assert.ThrowsAsync<OptimisticConcurrencyException>(() =>
            service.UpdateAsync(project.Id, request, expectedVersion: 99, default));

        Assert.Equal("version_mismatch", exception.Code);
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
        var service = new ProjectUseCase(repository, new StubCurrentUser(memberId), new StubClock(Now), unitOfWork);

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

        var result = await service.GetAsync(project.Id, new TaskFilter(Search: "  urgent  "), default);

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
        var published = Assert.IsType<ColumnChangedNotification>(Assert.Single(notifier.Published));
        Assert.Equal(project.Id, published.ProjectId);
        Assert.Same(response, published.Column);
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

        var exception = await Assert.ThrowsAsync<OptimisticConcurrencyException>(() =>
            service.UpdateColumnAsync(project.Id, column.Id, new UpdateColumn("Changed"), 2, default));

        Assert.Equal("version_mismatch", exception.Code);
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
        var published = Assert.IsType<TaskMovedNotification>(Assert.Single(notifier.Published));
        Assert.Same(response, published.Task);
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
        var token = new IssuedToken("access-token", Now.AddMinutes(30));
        var issuer = new TrackingTokenIssuer(token);
        var service = new SessionUseCase(users, new StubPasswordHasher(true), issuer);

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
        var issuer = new TrackingTokenIssuer(new IssuedToken("unused", Now));
        var service = new SessionUseCase(users, new StubPasswordHasher(false), issuer);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.CreateAsync(new CreateSession(user.Email, "wrong"), default));

        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Null(issuer.IssuedFor);
    }

    [Fact]
    public async Task ReportGenerate_ByNonMember_HidesProjectWithoutReadingReportData()
    {
        var project = CreateProject(Guid.NewGuid());
        var projects = RepositoryContaining(project);
        var dataSource = new FakeReportDataSource();
        var service = new ReportUseCase(projects, dataSource, [new StubReportExporter("pdf")],
            new StubCurrentUser(Guid.NewGuid()), new StubClock(Now));

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GenerateAsync(project.Id, "pdf", new TaskFilter(), default));

        Assert.Equal("project_not_found", exception.Code);
        Assert.Equal(0, dataSource.CallCount);
    }

    [Fact]
    public async Task ReportGenerate_WithNoMatchingTasks_ExportsAnEmptyReport()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var projects = RepositoryContaining(project);
        var data = new ProjectReportData(project.Id, project.Name, project.Description, project.StartDate,
            project.ExpectedEndDate, project.Status, Now, []);
        var dataSource = new FakeReportDataSource { Result = data };
        var exporter = new StubReportExporter("pdf", "application/pdf", "pdf");
        var service = new ReportUseCase(projects, dataSource, [exporter], new StubCurrentUser(ownerId), new StubClock(Now));

        var result = await service.GenerateAsync(project.Id, "pdf", new TaskFilter(Search: "  absent  "), default);

        Assert.Equal("absent", dataSource.LastFilter?.Search);
        Assert.Same(data, exporter.Exported);
        Assert.Equal("application/pdf", result.MediaType);
        Assert.EndsWith(".pdf", result.FileName);
    }

    [Fact]
    public async Task ReportGenerate_WithUnsupportedFormat_ListsRegisteredExporterInventory()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var service = new ReportUseCase(
            RepositoryContaining(project),
            new FakeReportDataSource(),
            [new StubReportExporter("pdf"), new StubReportExporter("xlsx")],
            new StubCurrentUser(ownerId),
            new StubClock(Now));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.GenerateAsync(project.Id, "csv", new TaskFilter(), default));

        Assert.Equal("Supported report formats are pdf and xlsx.", exception.Message);
    }

    private static ProjectUseCase CreateProjectService(FakeProjectRepository repository, Guid userId) =>
        new(repository, new StubCurrentUser(userId), new StubClock(Now), new TrackingUnitOfWork());

    private static BoardUseCase CreateBoardService(
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
