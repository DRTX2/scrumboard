using ScrumBoard.Domain.Users;

namespace ScrumBoard.Application.Ports.Outbound;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
