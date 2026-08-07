using ScrumBoard.Application.Models.Security;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.Application.Ports.Out;

public interface ITokenIssuer
{
    IssuedToken Issue(User user);
}
