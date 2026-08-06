namespace ScrumBoard.Application.Ports.Outbound;

public interface IPasswordHasher
{
    string DummyHash { get; }
    bool Verify(string password, string encodedHash);
}
