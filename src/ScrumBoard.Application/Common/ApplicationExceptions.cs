namespace ScrumBoard.Application.Common;

public abstract class ApplicationProblemException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code, string message) : ApplicationProblemException(code, message);
public sealed class ForbiddenException(string code, string message) : ApplicationProblemException(code, message);
public sealed class ConflictException(string code, string message) : ApplicationProblemException(code, message);
public sealed class PreconditionFailedException(string code, string message) : ApplicationProblemException(code, message);
public sealed class AuthenticationFailedException() : ApplicationProblemException("invalid_credentials", "Email or password is incorrect.");
