using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Application.Errors;
using ScrumBoard.Domain.Primitives;

namespace ScrumBoard.Adapters.Inbound.Infrastructure;

internal sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, string, string, Exception?> RejectedRequest =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, "RequestRejected"),
            "Request rejected with {Code} and trace {TraceId}");
    private static readonly Action<ILogger, string, Exception?> UnhandledError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, "UnhandledError"),
            "Unhandled request error with trace {TraceId}");

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            ValidationException appProblem => (StatusCodes.Status400BadRequest, appProblem.Code, "La solicitud no es válida."),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "invalid_credentials", "Autenticación fallida."),
            ForbiddenException appProblem => (StatusCodes.Status403Forbidden, appProblem.Code, "Acceso denegado."),
            NotFoundException appProblem => (StatusCodes.Status404NotFound, appProblem.Code, "No se encontró el recurso."),
            ConflictException appProblem => (StatusCodes.Status409Conflict, appProblem.Code, "La solicitud entra en conflicto con el estado actual."),
            OptimisticConcurrencyException appProblem => (StatusCodes.Status412PreconditionFailed,
                appProblem.Code == "version_mismatch" ? "etag_mismatch" : appProblem.Code, "El recurso cambió."),
            EntityTagRequiredException => (StatusCodes.Status428PreconditionRequired, "if_match_required", "Se requiere una precondición."),
            DomainException domainProblem => (StatusCodes.Status422UnprocessableEntity, domainProblem.Code, "La validación de negocio falló."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "invalid_request", "La solicitud no es válida."),
            _ => (StatusCodes.Status500InternalServerError, "unexpected_error", "Ocurrió un error inesperado.")
        };

        if (status >= 500) UnhandledError(logger, context.TraceIdentifier, exception);
        else RejectedRequest(logger, code, context.TraceIdentifier, exception);

        context.Response.StatusCode = status;
        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = DetailFor(status, code, exception),
            Type = TypeFor(status),
            Instance = context.Request.Path
        };
        details.Extensions["code"] = code;
        details.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(context.Response.Body, details, JsonOptions, cancellationToken);
        return true;
    }

    private static string TypeFor(int status) => status switch
    {
        400 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
        401 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2",
        403 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4",
        404 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
        409 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
        412 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.13",
        422 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.21",
        428 => "https://www.rfc-editor.org/rfc/rfc6585#section-3",
        _ => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1"
    };

    private static string DetailFor(int status, string code, Exception exception) => (status, code) switch
    {
        ( >= 500, _) => "El servidor no pudo completar la solicitud.",
        (_, "project_not_found") => "No se encontró el proyecto.",
        (_, "unsupported_report_format") => "El formato de reporte solicitado no es compatible.",
        _ => exception.Message
    };
}
