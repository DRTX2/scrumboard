using Microsoft.EntityFrameworkCore;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Repositories;

internal sealed class UserRepository(ScrumBoardDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
}
