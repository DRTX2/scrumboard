namespace ScrumBoard.Application.Errors;

public sealed class AuthenticationFailedException()
    : ApplicationProblemException("invalid_credentials", "El correo electrónico o la contraseña son incorrectos.");
