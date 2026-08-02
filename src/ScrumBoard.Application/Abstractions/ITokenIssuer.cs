using ScrumBoard.Application.Sessions;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.Application.Abstractions;

public interface ITokenIssuer
{
    SessionToken Issue(User user);
}
