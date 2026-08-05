namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Seed;

internal static class DemoSeedConstants
{
    public const string DevelopmentPepper = "scrumboard-development-pepper-only";
    public const string DemoPassword = "ScrumBoard123!";
    public static readonly DateTimeOffset CreatedAt = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid OwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid MemberId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ProjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid BacklogColumnId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid ProgressColumnId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid DoneColumnId = Guid.Parse("30000000-0000-0000-0000-000000000003");
}
