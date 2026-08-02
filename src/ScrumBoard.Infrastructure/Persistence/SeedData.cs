using Microsoft.EntityFrameworkCore;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Domain.Users;
using ScrumBoard.Infrastructure.Security;

namespace ScrumBoard.Infrastructure.Persistence;

internal static class SeedData
{
    public const string DevelopmentPepper = "scrumboard-development-pepper-only";
    public const string DemoPassword = "ScrumBoard123!";
    public static readonly Guid OwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid MemberId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ProjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid BacklogColumnId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid ProgressColumnId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid DoneColumnId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    public static void Configure(ModelBuilder modelBuilder)
    {
        var createdAt = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        var ownerHash = Pbkdf2PasswordHasher.HashWithSalt(
            DemoPassword,
            DevelopmentPepper,
            Convert.FromHexString("100102030405060708090A0B0C0D0E0F"));
        var memberHash = Pbkdf2PasswordHasher.HashWithSalt(
            DemoPassword,
            DevelopmentPepper,
            Convert.FromHexString("200102030405060708090A0B0C0D0E0F"));

        modelBuilder.Entity<User>().HasData(
            new { Id = OwnerId, Name = "Demo Owner", Email = "owner@scrumboard.local", PasswordHash = ownerHash, IsActive = true, CreatedAt = createdAt },
            new { Id = MemberId, Name = "Demo Member", Email = "member@scrumboard.local", PasswordHash = memberHash, IsActive = true, CreatedAt = createdAt });

        modelBuilder.Entity<Project>().HasData(new
        {
            Id = ProjectId,
            Name = "ScrumBoard Launch",
            Description = "Shared project used to demonstrate real-time collaboration.",
            StartDate = new DateOnly(2026, 7, 30),
            ExpectedEndDate = new DateOnly(2026, 8, 30),
            Status = ProjectStatus.Active,
            Version = 1L,
            BoardVersion = 1L,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });
        modelBuilder.Entity<ProjectMember>().HasData(
            new { ProjectId, UserId = OwnerId, Role = ProjectRole.Owner },
            new { ProjectId, UserId = MemberId, Role = ProjectRole.Member });

        modelBuilder.Entity<BoardColumn>().HasData(
            Column(BacklogColumnId, "Backlog", 1_024, createdAt),
            Column(ProgressColumnId, "In progress", 2_048, createdAt),
            Column(DoneColumnId, "Done", 3_072, createdAt));

        modelBuilder.Entity<TaskItem>().HasData(
            Task(Guid.Parse("40000000-0000-0000-0000-000000000001"), BacklogColumnId,
                "Review product backlog", "Prioritize the first sprint with the product owner.", TaskPriority.High, OwnerId, 1_024, createdAt),
            Task(Guid.Parse("40000000-0000-0000-0000-000000000002"), ProgressColumnId,
                "Build collaborative board", "Implement authenticated real-time updates.", TaskPriority.Critical, MemberId, 1_024, createdAt),
            Task(Guid.Parse("40000000-0000-0000-0000-000000000003"), DoneColumnId,
                "Define architecture", "Document ports, adapters and trade-offs.", TaskPriority.Medium, OwnerId, 1_024, createdAt));
    }

    private static object Column(Guid id, string name, long position, DateTimeOffset now) => new
    {
        Id = id,
        ProjectId,
        Name = name,
        Position = position,
        Version = 1L,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static object Task(
        Guid id,
        Guid columnId,
        string title,
        string description,
        TaskPriority priority,
        Guid assigneeId,
        long position,
        DateTimeOffset now) => new
    {
        Id = id,
        ProjectId,
        ColumnId = columnId,
        Title = title,
        Description = description,
        Priority = priority,
        AssigneeId = (Guid?)assigneeId,
        Position = position,
        Version = 1L,
        CreatedAt = now,
        UpdatedAt = now
    };
}
