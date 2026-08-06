using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;

public sealed class ScrumBoardDbContextFactory : IDesignTimeDbContextFactory<ScrumBoardDbContext>
{
    public ScrumBoardDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Database explicitly before running Entity Framework design-time commands.");
        var options = new DbContextOptionsBuilder<ScrumBoardDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ScrumBoardDbContext).Assembly.FullName))
            .Options;
        return new ScrumBoardDbContext(options);
    }
}
