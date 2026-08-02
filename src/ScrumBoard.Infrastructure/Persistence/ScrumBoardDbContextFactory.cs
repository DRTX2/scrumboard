using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScrumBoard.Infrastructure.Persistence;

public sealed class ScrumBoardDbContextFactory : IDesignTimeDbContextFactory<ScrumBoardDbContext>
{
    public ScrumBoardDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=scrumboard;Username=scrumboard;Password=scrumboard_dev";
        var options = new DbContextOptionsBuilder<ScrumBoardDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ScrumBoardDbContext).Assembly.FullName))
            .Options;
        return new ScrumBoardDbContext(options);
    }
}
