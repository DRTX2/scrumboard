namespace ScrumBoard.Application.Ports.Outbound;

public interface IPasswordHasher
{
    bool Verify(string password, string encodedHash);
}
