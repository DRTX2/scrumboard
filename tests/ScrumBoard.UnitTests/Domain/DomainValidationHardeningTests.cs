using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Primitives;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.UnitTests.Domain;

public sealed class DomainValidationHardeningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Column_WithNullName_ReturnsControlledDomainError()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new BoardColumn(Guid.NewGuid(), Guid.NewGuid(), null!, 1024, Now));

        Assert.Equal("required_value", exception.Code);
        Assert.Contains("obligatorio", exception.Message);
    }

    [Fact]
    public void Project_WithUndefinedStatus_ReturnsControlledDomainError()
    {
        var exception = Assert.Throws<DomainException>(() => new Project(
            Guid.NewGuid(),
            "Proyecto",
            null,
            new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 6),
            (ProjectStatus)999,
            Guid.NewGuid(),
            Now));

        Assert.Equal("invalid_enum_value", exception.Code);
    }

    [Fact]
    public void Task_WithoutAssignee_ReturnsControlledDomainError()
    {
        var exception = Assert.Throws<DomainException>(() => new TaskItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tarea",
            null,
            TaskPriority.Medium,
            Guid.Empty,
            null,
            1024,
            Now));

        Assert.Equal("required_identifier", exception.Code);
    }
}
