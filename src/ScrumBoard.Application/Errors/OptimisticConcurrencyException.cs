namespace ScrumBoard.Application.Errors;

public sealed class OptimisticConcurrencyException(string code, string message, Exception? innerException = null)
    : ApplicationProblemException(code, message, innerException);
