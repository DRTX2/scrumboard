namespace ScrumBoard.Application.Errors;

public sealed class ForbiddenException(string code, string message) : ApplicationProblemException(code, message);
