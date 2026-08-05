using Microsoft.EntityFrameworkCore;
using ScrumBoard.Domain.Users;
using ScrumBoard.Infrastructure.Adapters.Outbound.Security;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Seed;

internal static class DemoUserSeed
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var ownerHash = Pbkdf2PasswordHasher.HashWithSalt(
            DemoSeedConstants.DemoPassword,
            DemoSeedConstants.DevelopmentPepper,
            Convert.FromHexString("100102030405060708090A0B0C0D0E0F"));
        var memberHash = Pbkdf2PasswordHasher.HashWithSalt(
            DemoSeedConstants.DemoPassword,
            DemoSeedConstants.DevelopmentPepper,
            Convert.FromHexString("200102030405060708090A0B0C0D0E0F"));

        modelBuilder.Entity<User>().HasData(
            new
            {
                Id = DemoSeedConstants.OwnerId,
                Name = "Demo Owner",
                Email = "owner@scrumboard.local",
                PasswordHash = ownerHash,
                IsActive = true,
                DemoSeedConstants.CreatedAt
            },
            new
            {
                Id = DemoSeedConstants.MemberId,
                Name = "Demo Member",
                Email = "member@scrumboard.local",
                PasswordHash = memberHash,
                IsActive = true,
                DemoSeedConstants.CreatedAt
            });
    }
}
