using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
builder.Services.AddDbContext<ScrumBoardDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsAssembly(typeof(ScrumBoardDbContext).Assembly.FullName)));

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var database = scope.ServiceProvider.GetRequiredService<ScrumBoardDbContext>();
var pending = (await database.Database.GetPendingMigrationsAsync()).ToArray();
if (pending.Length == 0)
{
    Console.WriteLine("Database is already up to date.");
    return;
}

Console.WriteLine($"Applying {pending.Length} migration(s): {string.Join(", ", pending)}");
await database.Database.MigrateAsync();
Console.WriteLine("Database migrations completed successfully.");
