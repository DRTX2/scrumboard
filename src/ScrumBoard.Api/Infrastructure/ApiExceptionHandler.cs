using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumBoard.Application.Common;
using ScrumBoard.Domain.Common;

namespace ScrumBoard.Api.Infrastructure;

internal sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
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
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "invalid_credentials", "Authentication failed."),
            ForbiddenException appProblem => (StatusCodes.Status403Forbidden, appProblem.Code, "Access denied."),
            NotFoundException appProblem => (StatusCodes.Status404NotFound, appProblem.Code, "Resource not found."),
            ConflictException appProblem => (StatusCodes.Status409Conflict, appProblem.Code, "The request conflicts with current state."),
            PreconditionFailedException appProblem => (StatusCodes.Status412PreconditionFailed, appProblem.Code, "The resource has changed."),
            PreconditionRequiredException appProblem => (StatusCodes.Status428PreconditionRequired, appProblem.Code, "A precondition is required."),
            DomainException domainProblem => (StatusCodes.Status422UnprocessableEntity, domainProblem.Code, "Business validation failed."),
            DbUpdateConcurrencyException => (StatusCodes.Status412PreconditionFailed, "concurrent_update", "The resource has changed."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "invalid_request", "The request is invalid."),
            _ => (StatusCodes.Status500InternalServerError, "unexpected_error", "An unexpected error occurred.")
        };

        if (status >= 500) UnhandledError(logger, context.TraceIdentifier, exception);
        else RejectedRequest(logger, code, context.TraceIdentifier, exception);

        context.Response.StatusCode = status;
        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "The server could not complete the request." : exception.Message,
            Type = TypeFor(status),
            Instance = context.Request.Path
        };
        details.Extensions["code"] = code;
        details.Extensions["traceId"] = context.TraceIdentifier;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = details,
            Exception = exception
        });
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
}
