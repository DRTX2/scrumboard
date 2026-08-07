namespace ScrumBoard.Application.Ports.Out;

public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAuthenticated { get; }
}
