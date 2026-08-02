using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ScrumBoard.Api.Infrastructure;

internal static class AuthenticationProblemResponses
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, int status, string title, string code, string type)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails { Status = status, Title = title, Type = type };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions), context.RequestAborted);
    }
}
