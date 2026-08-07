namespace ScrumBoard.Application.Ports.Out;

public interface IPasswordHasher
{
    string DummyHash { get; }
    bool Verify(string password, string encodedHash);
}
