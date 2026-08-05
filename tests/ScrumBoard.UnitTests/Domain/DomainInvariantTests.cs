using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Primitives;
using ScrumBoard.Domain.Ordering;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.UnitTests.Domain;

public sealed class DomainInvariantTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Between_WithNoNeighbors_ReturnsInitialStep() =>
        Assert.Equal(OrderPosition.Step, OrderPosition.Between(null, null));

    [Theory]
    [InlineData(null, 1024L, 512L)]
    [InlineData(1024L, 2048L, 1536L)]
    [InlineData(2048L, null, 3072L)]
    public void Between_WithAvailableSpace_ReturnsStableMidpoint(long? previous, long? next, long expected) =>
        Assert.Equal(expected, OrderPosition.Between(previous, next));

    [Theory]
    [InlineData(1024L, 1025L)]
    [InlineData(2048L, 1024L)]
    [InlineData(1024L, 1024L)]
    public void Between_WithoutValidSpace_RequiresRebalance(long previous, long next)
    {
        var exception = Assert.Throws<DomainException>(() => OrderPosition.Between(previous, next));

        Assert.Equal("order_rebalance_required", exception.Code);
    }

    [Fact]
    public void Between_WhenAppendWouldOverflow_RequiresRebalance()
    {
        var exception = Assert.Throws<DomainException>(() => OrderPosition.Between(long.MaxValue, null));

        Assert.Equal("order_rebalance_required", exception.Code);
    }

    [Fact]
    public void Rebalance_ReturnsStrictlyIncreasingStepSizedPositions()
    {
        var positions = OrderPosition.Rebalance(4);

        Assert.Equal(new long[] { 1024, 2048, 3072, 4096 }, positions);
    }

    [Fact]
    public void Project_WithEndBeforeStart_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() => CreateProject(
            new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 1)));

        Assert.Equal("invalid_project_dates", exception.Code);
    }

    [Fact]
    public void Project_Creation_AddsOwnerMembershipAndInitialVersions()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId: ownerId);

        var membership = Assert.Single(project.Members);
        Assert.Equal(ownerId, membership.UserId);
        Assert.Equal(ProjectRole.Owner, membership.Role);
        Assert.Equal(1, project.Version);
        Assert.Equal(1, project.BoardVersion);
    }

    [Fact]
    public void Project_AddingExistingMember_IsRejectedWithoutChangingVersion()
    {
        var ownerId = Guid.NewGuid();
        var project = CreateProject(ownerId: ownerId);

        var exception = Assert.Throws<DomainException>(() =>
            project.AddMember(ownerId, ProjectRole.Member, InitialTime.AddMinutes(1)));

        Assert.Equal("member_exists", exception.Code);
        Assert.Equal(1, project.Version);
        Assert.Single(project.Members);
    }

    [Fact]
    public void Project_MembersCannotBeMutatedOutsideTheAggregate()
    {
        var project = CreateProject();
        var members = Assert.IsAssignableFrom<ICollection<ProjectMember>>(project.Members);

        Assert.True(members.IsReadOnly);
        Assert.Throws<NotSupportedException>(() =>
            members.Add(new ProjectMember(project.Id, Guid.NewGuid(), ProjectRole.Member)));
    }

    [Fact]
    public void Project_UpdateAndBoardTouch_AdvanceIndependentVersions()
    {
        var project = CreateProject();
        var updateTime = InitialTime.AddMinutes(1);
        var boardTime = InitialTime.AddMinutes(2);

        project.Update("  Renamed  ", "  description  ", new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 1), ProjectStatus.Completed, updateTime);
        project.TouchBoard(boardTime);

        Assert.Equal("Renamed", project.Name);
        Assert.Equal("description", project.Description);
        Assert.Equal(2, project.Version);
        Assert.Equal(2, project.BoardVersion);
        Assert.Equal(boardTime, project.UpdatedAt);
    }

    [Fact]
    public void Column_DeleteIsRejectedWhenTasksRemain()
    {
        var exception = Assert.Throws<DomainException>(() => BoardColumn.EnsureCanDelete(true));

        Assert.Equal("column_not_empty", exception.Code);
    }

    [Fact]
    public void Column_UpdateAndMove_IncrementVersionAndTimestamp()
    {
        var column = new BoardColumn(Guid.NewGuid(), Guid.NewGuid(), "Backlog", 1024, InitialTime);

        column.Update("  Ready  ", InitialTime.AddMinutes(1));
        column.MoveTo(2048, InitialTime.AddMinutes(2));

        Assert.Equal("Ready", column.Name);
        Assert.Equal(2048, column.Position);
        Assert.Equal(3, column.Version);
        Assert.Equal(InitialTime.AddMinutes(2), column.UpdatedAt);
    }

    [Fact]
    public void Task_UpdateNormalizesValuesAndAdvancesVersion()
    {
        var task = CreateTask();
        var assigneeId = Guid.NewGuid();
        var dueDate = new DateOnly(2026, 8, 20);

        task.Update("  Updated title  ", "   ", TaskPriority.Critical, assigneeId, dueDate,
            InitialTime.AddMinutes(1));

        Assert.Equal("Updated title", task.Title);
        Assert.Null(task.Description);
        Assert.Equal(TaskPriority.Critical, task.Priority);
        Assert.Equal(assigneeId, task.AssigneeId);
        Assert.Equal(dueDate, task.DueDate);
        Assert.Equal(2, task.Version);
    }

    [Fact]
    public void Task_MoveChangesOnlyBoardPlacementAndVersion()
    {
        var task = CreateTask();
        var targetColumnId = Guid.NewGuid();

        task.Move(targetColumnId, 3072, InitialTime.AddMinutes(1));

        Assert.Equal(targetColumnId, task.ColumnId);
        Assert.Equal(3072, task.Position);
        Assert.Equal(2, task.Version);
        Assert.Equal(InitialTime, task.CreatedAt);
    }

    [Fact]
    public void RequiredTextOverMaximumLength_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new BoardColumn(Guid.NewGuid(), Guid.NewGuid(), new string('x', 101), 1024, InitialTime));

        Assert.Equal("value_too_long", exception.Code);
    }

    [Fact]
    public void User_NormalizesNameAndEmail()
    {
        var user = new User(Guid.NewGuid(), "  Ada Lovelace  ", "  ADA@Example.COM  ", "hash", InitialTime);

        Assert.Equal("Ada Lovelace", user.Name);
        Assert.Equal("ada@example.com", user.Email);
    }

    private static Project CreateProject(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        Guid? ownerId = null) =>
        new(Guid.NewGuid(), "Project", null, startDate ?? new DateOnly(2026, 8, 1),
            endDate ?? new DateOnly(2026, 9, 1), ProjectStatus.Active, ownerId ?? Guid.NewGuid(), InitialTime);

    private static TaskItem CreateTask() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Task", "Description", TaskPriority.Medium,
            null, null, 1024, InitialTime);
}
