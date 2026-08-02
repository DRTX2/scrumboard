using ScrumBoard.Domain.Common;

namespace ScrumBoard.Domain.Users;

public sealed class User
{
    private User() { }

    public User(Guid id, string name, string email, string passwordHash, DateTimeOffset createdAt)
    {
        Id = id;
        Name = Guard.Required(name, nameof(name), 120);
        Email = Guard.Required(email, nameof(email), 254).ToLowerInvariant();
        PasswordHash = Guard.Required(passwordHash, nameof(passwordHash), 512);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
}
