namespace ScrumBoard.Application.Errors;

public sealed class ConflictException(string code, string message, Exception? innerException = null)
    : ApplicationProblemException(code, message, innerException);
