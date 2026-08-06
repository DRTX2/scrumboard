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
    public async Task ProjectList_NormalizesSearchAndMapsValidatedSortingBeforeRepositoryCall()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeProjectRepository();
        var service = CreateProjectService(repository, userId);

        await service.ListAsync(new ProjectListQuery(2, 100, "  roadmap  ", "name", "desc"), default);

        Assert.Equal(userId, repository.LastListUserId);
        Assert.Equal(new ProjectSearchCriteria(2, 100, "roadmap", ProjectSortField.Name, SortDirection.Descending),
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

        var result = await service.GetAsync(project.Id, new TaskFilter(Search: "  urgent  "), 20, default);

        Assert.Same(boards.Snapshot, result);
        Assert.Equal("urgent", boards.LastFilter?.Search);
        Assert.Equal(20, boards.LastTaskLimit);
        Assert.Equal(0, projects.FindCount);
    }

    [Fact]
    public async Task ColumnTasks_NormalizesFilterAndPassesValidatedLimitToRepository()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var column = new BoardColumn(Guid.NewGuid(), project.Id, "Backlog", 1024, Now);
        var cursorId = Guid.NewGuid();
        var boards = new FakeBoardRepository
        {
            Page = new TaskPage([], 0, false, project.BoardVersion)
        };
        boards.Columns.Add(column);
        var service = CreateBoardService(RepositoryContaining(project), boards, ownerId);

        var result = await service.GetTasksAsync(project.Id, column.Id, new TaskFilter(Search: "  urgent  "),
            50, 1024, cursorId, project.BoardVersion, default);

        Assert.Same(boards.Page, result);
        Assert.Equal("urgent", boards.LastFilter?.Search);
        Assert.Equal(50, boards.LastTaskLimit);
        Assert.Equal(1024, boards.LastAfterPosition);
        Assert.Equal(cursorId, boards.LastAfterTaskId);
    }

    [Fact]
    public async Task ColumnTasks_ByNonMember_HidesProjectBeforeReadingTasks()
    {
        var project = CreateProject(Guid.NewGuid());
        var boards = new FakeBoardRepository { IsVisible = false };
        var service = CreateBoardService(RepositoryContaining(project), boards, Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetTasksAsync(project.Id, Guid.NewGuid(), new TaskFilter(), 20, null, null, 1, default));

        Assert.Equal("project_not_found", exception.Code);
        Assert.Null(boards.LastFilter);
    }

    [Fact]
    public async Task ColumnTasks_WithStaleBoardVersion_RejectsContinuation()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var column = new BoardColumn(Guid.NewGuid(), project.Id, "Backlog", 1024, Now);
        var boards = new FakeBoardRepository
        {
            Page = new TaskPage([], 0, false, project.BoardVersion + 1)
        };
        boards.Columns.Add(column);
        var service = CreateBoardService(RepositoryContaining(project), boards, ownerId);

        var exception = await Assert.ThrowsAsync<OptimisticConcurrencyException>(() =>
            service.GetTasksAsync(project.Id, column.Id, new TaskFilter(), 20, null, null,
                project.BoardVersion, default));

        Assert.Equal("version_mismatch", exception.Code);
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
        Assert.Equal(0, boards.FullTaskQueryCount);
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
        Assert.Equal(1, boards.FullTaskQueryCount);
    }

    [Fact]
    public async Task SessionCreate_NormalizesEmailAndIssuesTokenForVerifiedUser()
    {
        var user = new User(Guid.NewGuid(), "Ada", "ada@example.com", "encoded", Now);
        var users = new FakeUserRepository { User = user };
        var token = new IssuedToken("access-token", Now.AddMinutes(30));
        var issuer = new TrackingTokenIssuer(token);
        var hasher = new TrackingPasswordHasher(true);
        var service = new SessionUseCase(users, hasher, issuer);

        var response = await service.CreateAsync(new CreateSession("  ADA@EXAMPLE.COM  ", "password"), default);

        Assert.Equal("ada@example.com", users.RequestedEmail);
        Assert.Equal(("password", user.PasswordHash), Assert.Single(hasher.Verifications));
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
        var hasher = new TrackingPasswordHasher(false);
        var service = new SessionUseCase(users, hasher, issuer);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.CreateAsync(new CreateSession(user.Email, "wrong"), default));

        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Equal(("wrong", user.PasswordHash), Assert.Single(hasher.Verifications));
        Assert.Null(issuer.IssuedFor);
    }

    [Fact]
    public async Task SessionCreate_WithMissingUser_VerifiesDummyHashOnceAndReturnsInvalidCredentials()
    {
        var hasher = new TrackingPasswordHasher(true, "configured-dummy-hash");
        var issuer = new TrackingTokenIssuer(new IssuedToken("unused", Now));
        var service = new SessionUseCase(new FakeUserRepository(), hasher, issuer);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.CreateAsync(new CreateSession("missing@example.com", "password"), default));

        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Equal(("password", hasher.DummyHash), Assert.Single(hasher.Verifications));
        Assert.Null(issuer.IssuedFor);
    }

    [Fact]
    public async Task SessionCreate_WithInactiveUser_VerifiesDummyHashOnceAndReturnsInvalidCredentials()
    {
        var user = new User(Guid.NewGuid(), "Ada", "ada@example.com", "real-hash", Now);
        typeof(User).GetProperty(nameof(User.IsActive))!.SetValue(user, false);
        var hasher = new TrackingPasswordHasher(true, "configured-dummy-hash");
        var issuer = new TrackingTokenIssuer(new IssuedToken("unused", Now));
        var service = new SessionUseCase(new FakeUserRepository { User = user }, hasher, issuer);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.CreateAsync(new CreateSession(user.Email, "password"), default));

        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Equal(("password", hasher.DummyHash), Assert.Single(hasher.Verifications));
        Assert.Null(issuer.IssuedFor);
    }

    [Fact]
    public async Task ReportGenerate_WhenAuthorizedQueryFindsNoMembership_HidesProject()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dataSource = new FakeReportDataSource();
        var service = new ReportUseCase(dataSource, [new StubReportExporter("pdf")],
            new StubCurrentUser(userId), new StubClock(Now));

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GenerateAsync(projectId, "pdf", new TaskFilter(), default));

        Assert.Equal("project_not_found", exception.Code);
        Assert.Equal(1, dataSource.CallCount);
        Assert.Equal(userId, dataSource.LastUserId);
    }

    [Fact]
    public async Task ReportGenerate_WithNoMatchingTasks_ExportsAnEmptyReport()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var data = new ProjectReportData(project.Id, project.Name, project.Description, project.StartDate,
            project.ExpectedEndDate, project.Status, Now, []);
        var dataSource = new FakeReportDataSource { Result = data };
        var exporter = new StubReportExporter("pdf", "application/pdf", "pdf");
        var service = new ReportUseCase(dataSource, [exporter], new StubCurrentUser(ownerId), new StubClock(Now));

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
        var data = new ProjectReportData(project.Id, project.Name, project.Description, project.StartDate,
            project.ExpectedEndDate, project.Status, Now, []);
        var dataSource = new FakeReportDataSource { Result = data };
        var service = new ReportUseCase(
            dataSource,
            [new StubReportExporter("pdf"), new StubReportExporter("xlsx")],
            new StubCurrentUser(ownerId),
            new StubClock(Now));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.GenerateAsync(project.Id, "csv", new TaskFilter(), default));

        Assert.Equal("Los formatos de reporte admitidos son pdf y xlsx.", exception.Message);
        Assert.Equal("unsupported_report_format", exception.Code);
        Assert.Equal(0, dataSource.CallCount);
    }

    [Fact]
    public async Task ReportGenerate_AtSynchronousRowLimit_ExportsReportAndRequestsOneExtraRow()
    {
        var userId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var data = new ProjectReportData(Guid.NewGuid(), "Project", null, new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31), ProjectStatus.Active, Now,
            new CountOnlyReadOnlyList<ProjectReportTask>(ReportUseCase.MaximumSynchronousTaskRows));
        var dataSource = new FakeReportDataSource { Result = data };
        var exporter = new StubReportExporter("pdf", fileExtension: "pdf");
        var service = new ReportUseCase(dataSource, [exporter], new StubCurrentUser(userId), new StubClock(Now));

        await service.GenerateAsync(data.ProjectId, "pdf", new TaskFilter(), cancellation.Token);

        Assert.Same(data, exporter.Exported);
        Assert.Equal(ReportUseCase.MaximumSynchronousTaskRows + 1, dataSource.LastTaskRowLimit);
        Assert.Equal(cancellation.Token, dataSource.LastCancellationToken);
    }

    [Fact]
    public async Task ReportGenerate_OneRowOverSynchronousLimit_ReturnsControlledValidationBeforeExport()
    {
        var userId = Guid.NewGuid();
        var data = new ProjectReportData(Guid.NewGuid(), "Project", null, new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31), ProjectStatus.Active, Now,
            new CountOnlyReadOnlyList<ProjectReportTask>(ReportUseCase.MaximumSynchronousTaskRows + 1));
        var dataSource = new FakeReportDataSource { Result = data };
        var exporter = new StubReportExporter("pdf");
        var service = new ReportUseCase(dataSource, [exporter], new StubCurrentUser(userId), new StubClock(Now));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.GenerateAsync(data.ProjectId, "pdf", new TaskFilter(), default));

        Assert.Equal("report_too_large", exception.Code);
        Assert.Equal("El reporte no puede exportarse de forma síncrona porque supera el límite de 10.000 tareas.",
            exception.Message);
        Assert.Null(exporter.Exported);
        Assert.Equal(ReportUseCase.MaximumSynchronousTaskRows + 1, dataSource.LastTaskRowLimit);
    }

    [Fact]
    public async Task ReportGenerate_SelectsNewlyRegisteredExporterWithoutUseCaseChanges()
    {
        var userId = Guid.NewGuid();
        var project = CreateProject(userId);
        var data = new ProjectReportData(project.Id, project.Name, project.Description, project.StartDate,
            project.ExpectedEndDate, project.Status, Now, []);
        var dataSource = new FakeReportDataSource { Result = data };
        var csv = new StubReportExporter("csv", "text/csv", "csv", [9, 8, 7]);
        var service = new ReportUseCase(dataSource,
            [new StubReportExporter("pdf"), new StubReportExporter("xlsx"), csv],
            new StubCurrentUser(userId), new StubClock(Now));

        var result = await service.GenerateAsync(project.Id, "CSV", new TaskFilter(), default);

        Assert.Same(data, csv.Exported);
        Assert.Equal("text/csv", result.MediaType);
        Assert.Equal([9, 8, 7], result.Content);
    }

    [Fact]
    public async Task ReportGenerate_WithUnsafeEmptyProjectName_UsesFallbackAndUtcTimestamp()
    {
        var userId = Guid.NewGuid();
        var generatedAt = new DateTimeOffset(2026, 8, 5, 23, 47, 0, TimeSpan.FromHours(5));
        var data = new ProjectReportData(Guid.NewGuid(), "***", null, new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31), ProjectStatus.Active, generatedAt, []);
        var service = new ReportUseCase(
            new FakeReportDataSource { Result = data },
            [new StubReportExporter("pdf", fileExtension: "pdf")],
            new StubCurrentUser(userId),
            new StubClock(generatedAt));

        var result = await service.GenerateAsync(data.ProjectId, "pdf", new TaskFilter(), default);

        Assert.Equal("reporte-proyecto-20260805-1847.pdf", result.FileName);
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
        new(Guid.NewGuid(), projectId, columnId, "Task", null, TaskPriority.Medium, Guid.NewGuid(), null, position, Now);
}
