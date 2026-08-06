using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Inbound.Projects;
using ScrumBoard.Application.UseCases.Boards;
using ScrumBoard.Application.UseCases.Projects;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.UnitTests.Application.UseCases;

public sealed class ApplicationValidationHardeningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ProjectList_WithOutOfRangePagination_IsRejected(int page, int pageSize)
    {
        var service = new ProjectUseCase(
            new FakeProjectRepository(),
            new StubCurrentUser(Guid.NewGuid()),
            new StubClock(Now),
            new TrackingUnitOfWork());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ListAsync(new ProjectListQuery(page, pageSize), default));

        Assert.Equal("value_out_of_range", exception.Code);
    }

    [Fact]
    public async Task TaskPage_WithPartialCursor_IsRejectedBeforeRepositoryAccess()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var boards = new FakeBoardRepository();
        var service = CreateBoardService(project, boards, ownerId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.GetTasksAsync(
            project.Id,
            Guid.NewGuid(),
            new TaskFilter(),
            20,
            1024,
            null,
            project.BoardVersion,
            default));

        Assert.Equal("invalid_page_cursor", exception.Code);
        Assert.Null(boards.LastFilter);
    }

    [Fact]
    public async Task MoveTask_WithNonConsecutiveNeighbors_IsRejected()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var column = new BoardColumn(Guid.NewGuid(), project.Id, "Pendiente", 1024, Now);
        var moved = Task(project, column, ownerId, 1024);
        var first = Task(project, column, ownerId, 2048);
        var middle = Task(project, column, ownerId, 3072);
        var last = Task(project, column, ownerId, 4096);
        var boards = new FakeBoardRepository();
        boards.Columns.Add(column);
        boards.Tasks.AddRange([moved, first, middle, last]);
        var service = CreateBoardService(project, boards, ownerId);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.MoveTaskAsync(
            project.Id,
            moved.Id,
            new MoveTask(column.Id, last.Id, first.Id),
            project.BoardVersion,
            default));

        Assert.Equal("invalid_order_neighbors", exception.Code);
    }

    [Fact]
    public async Task CreateTask_WithEmptyAssignee_IsRejectedBeforeMembershipLookup()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId);
        var boards = new FakeBoardRepository();
        var service = CreateBoardService(project, boards, ownerId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateTaskAsync(
            project.Id,
            new CreateTask(Guid.NewGuid(), "Tarea", null, TaskPriority.Low, Guid.Empty, null),
            default));

        Assert.Equal("invalid_identifier", exception.Code);
        Assert.Null(boards.AddedTask);
    }

    private static BoardUseCase CreateBoardService(Project project, FakeBoardRepository boards, Guid userId)
    {
        var projects = new FakeProjectRepository();
        projects.Projects.Add(project.Id, project);
        return new BoardUseCase(
            projects,
            boards,
            new StubCurrentUser(userId),
            new StubClock(Now),
            new TrackingUnitOfWork(),
            new TrackingNotifier());
    }

    private static Project CreateProject(Guid ownerId) => new(
        Guid.NewGuid(),
        "Proyecto",
        null,
        new DateOnly(2026, 8, 5),
        new DateOnly(2026, 8, 30),
        ProjectStatus.Active,
        ownerId,
        Now);

    private static TaskItem Task(Project project, BoardColumn column, Guid assigneeId, long position) => new(
        Guid.NewGuid(), project.Id, column.Id, "Tarea", null, TaskPriority.Medium, assigneeId, null, position, Now);
}
