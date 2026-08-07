using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ScrumBoard.Adapters.Outbound.Security;

namespace ScrumBoard.Adapters.Outbound.Persistence.Seed;

public static class BootstrapAdminSeeder
{
    public static async Task ApplyAsync(
        ScrumBoardDbContext dbContext,
        string name,
        string email,
        string password,
        string pepper,
        bool disableDemoMember,
        bool removeDemoWorkspace,
        CancellationToken cancellationToken = default)
    {
        var passwordHash = Pbkdf2PasswordHasher.HashWithSalt(
            password,
            pepper,
            RandomNumberGenerator.GetBytes(16));
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var updated = await dbContext.Users
            .Where(user => user.Id == DemoSeedConstants.OwnerId)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(user => user.Name, name.Trim())
                    .SetProperty(user => user.Email, normalizedEmail)
                    .SetProperty(user => user.PasswordHash, passwordHash)
                    .SetProperty(user => user.IsActive, true),
                cancellationToken);
        if (updated != 1)
            throw new InvalidOperationException("The bootstrap administrator seed record was not found.");

        await dbContext.Users
            .Where(user => user.Id == DemoSeedConstants.MemberId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsActive, !disableDemoMember),
                cancellationToken);

        if (removeDemoWorkspace)
        {
            await dbContext.Projects
                .Where(project => project.Id == DemoSeedConstants.ProjectId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
