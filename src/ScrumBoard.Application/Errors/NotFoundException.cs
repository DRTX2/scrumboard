namespace ScrumBoard.Application.Errors;

public sealed class NotFoundException(string code, string message) : ApplicationProblemException(code, message);
