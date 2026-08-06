namespace ScrumBoard.Application.Errors;

public sealed class ValidationException(string code, string message) : ApplicationProblemException(code, message);
