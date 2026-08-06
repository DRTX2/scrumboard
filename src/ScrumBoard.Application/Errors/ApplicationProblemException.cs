namespace ScrumBoard.Application.Errors;

public abstract class ApplicationProblemException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
