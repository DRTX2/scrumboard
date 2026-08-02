namespace ScrumBoard.Application.Abstractions;

public interface IPasswordHasher
{
    bool Verify(string password, string encodedHash);
}
