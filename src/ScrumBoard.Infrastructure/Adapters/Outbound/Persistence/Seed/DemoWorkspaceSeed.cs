using Microsoft.EntityFrameworkCore;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Seed;

internal static class DemoWorkspaceSeed
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>().HasData(new
        {
            Id = DemoSeedConstants.ProjectId,
            Name = "ScrumBoard Launch",
            Description = "Shared project used to demonstrate real-time collaboration.",
            StartDate = new DateOnly(2026, 7, 30),
            ExpectedEndDate = new DateOnly(2026, 8, 30),
            Status = ProjectStatus.Active,
            Version = 1L,
            BoardVersion = 1L,
            DemoSeedConstants.CreatedAt,
            UpdatedAt = DemoSeedConstants.CreatedAt
        });
        modelBuilder.Entity<ProjectMember>().HasData(
            new { ProjectId = DemoSeedConstants.ProjectId, UserId = DemoSeedConstants.OwnerId, Role = ProjectRole.Owner },
            new { ProjectId = DemoSeedConstants.ProjectId, UserId = DemoSeedConstants.MemberId, Role = ProjectRole.Member });
        modelBuilder.Entity<BoardColumn>().HasData(
            Column(DemoSeedConstants.BacklogColumnId, "Backlog", 1_024),
            Column(DemoSeedConstants.ProgressColumnId, "In progress", 2_048),
            Column(DemoSeedConstants.DoneColumnId, "Done", 3_072));
        modelBuilder.Entity<TaskItem>().HasData(
            Task(Guid.Parse("40000000-0000-0000-0000-000000000001"), DemoSeedConstants.BacklogColumnId,
                "Review product backlog", "Prioritize the first sprint with the product owner.", TaskPriority.High, DemoSeedConstants.OwnerId),
            Task(Guid.Parse("40000000-0000-0000-0000-000000000002"), DemoSeedConstants.ProgressColumnId,
                "Build collaborative board", "Implement authenticated real-time updates.", TaskPriority.Critical, DemoSeedConstants.MemberId),
            Task(Guid.Parse("40000000-0000-0000-0000-000000000003"), DemoSeedConstants.DoneColumnId,
                "Define architecture", "Document ports, adapters and trade-offs.", TaskPriority.Medium, DemoSeedConstants.OwnerId));
    }

    private static object Column(Guid id, string name, long position) => new
    {
        Id = id,
        ProjectId = DemoSeedConstants.ProjectId,
        Name = name,
        Position = position,
        Version = 1L,
        DemoSeedConstants.CreatedAt,
        UpdatedAt = DemoSeedConstants.CreatedAt
    };

    private static object Task(
        Guid id,
        Guid columnId,
        string title,
        string description,
        TaskPriority priority,
        Guid assigneeId) => new
        {
            Id = id,
            ProjectId = DemoSeedConstants.ProjectId,
            ColumnId = columnId,
            Title = title,
            Description = description,
            Priority = priority,
            AssigneeId = assigneeId,
            DueDate = (DateOnly?)null,
            Position = 1_024L,
            Version = 1L,
            DemoSeedConstants.CreatedAt,
            UpdatedAt = DemoSeedConstants.CreatedAt
        };
}
