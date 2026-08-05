namespace ScrumBoard.Application.Errors;

public abstract class ApplicationProblemException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code, string message) : ApplicationProblemException(code, message);
public sealed class ForbiddenException(string code, string message) : ApplicationProblemException(code, message);
public sealed class ConflictException(string code, string message) : ApplicationProblemException(code, message);
public sealed class OptimisticConcurrencyException(string code, string message, Exception? innerException = null)
    : ApplicationProblemException(code, message, innerException);
public sealed class AuthenticationFailedException() : ApplicationProblemException("invalid_credentials", "Email or password is incorrect.");
