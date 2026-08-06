using Microsoft.EntityFrameworkCore;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Seed;

internal static class DemoUserSeed
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new
            {
                Id = DemoSeedConstants.OwnerId,
                Name = "Demo Owner",
                Email = "owner@scrumboard.local",
                PasswordHash = "pbkdf2-sha512.210000.EAECAwQFBgcICQoLDA0ODw==./lanLqoVjc6fLDiztMJR6F8AdOXAQlpTUuHreEVVtlk=",
                IsActive = true,
                DemoSeedConstants.CreatedAt
            },
            new
            {
                Id = DemoSeedConstants.MemberId,
                Name = "Demo Member",
                Email = "member@scrumboard.local",
                PasswordHash = "pbkdf2-sha512.210000.IAECAwQFBgcICQoLDA0ODw==.sXzKpx2ClnU/GVOq9jn9613AU7KMaVn8zJXl5eGLGSw=",
                IsActive = true,
                DemoSeedConstants.CreatedAt
            });
    }
}
