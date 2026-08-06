using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using ScrumBoard.Application.Errors;
using ScrumBoard.Api.Infrastructure.Idempotency;

namespace ScrumBoard.Api.Infrastructure;

internal sealed class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    private const string HeaderName = "Idempotency-Key";
    private static readonly Action<ILogger, Exception?> AbortFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, "IdempotencyAbortFailed"),
            "Could not abort an idempotent request cleanly");

    public async Task InvokeAsync(
        HttpContext context,
        IIdempotencyCoordinator coordinator,
        PostCommitActionQueue postCommitActions,
        TimeProvider timeProvider)
    {
        var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotentAttribute>() is null ||
            context.User.Identity?.IsAuthenticated is not true ||
            !Guid.TryParse(userIdClaim, out var userId) ||
            !context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            await next(context);
            return;
        }

        if (values.Count != 1) throw new BadHttpRequestException("Se requiere exactamente una cabecera Idempotency-Key.");
        var key = values[0]?.Trim() ?? string.Empty;
        if (key.Length is 0 or > 100)
        {
            throw new BadHttpRequestException("Idempotency-Key debe contener entre 1 y 100 caracteres.");
        }
        context.Request.EnableBuffering();
        await using var requestBody = new MemoryStream();
        await context.Request.Body.CopyToAsync(requestBody, context.RequestAborted);
        context.Request.Body.Position = 0;
        var operation = CanonicalOperation(context);
        var hash = RequestHash(context, operation, requestBody.GetBuffer().AsSpan(0, (int)requestBody.Length));
        var now = timeProvider.GetUtcNow();
        var reservation = await coordinator.ReserveAsync(userId, operation, key, hash, now, now.AddMinutes(5),
            context.RequestAborted);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(reservation.RequestHash),
                Convert.FromHexString(hash)))
        {
            throw new ConflictException("idempotency_key_reused", "La clave de idempotencia ya se usó con otra solicitud.");
        }
        if (reservation.State is IdempotencyReservationState.InProgress)
            throw new ConflictException("request_in_progress", "Todavía se está procesando una solicitud con esta clave de idempotencia.");
        if (reservation.State is IdempotencyReservationState.Completed)
        {
            await ReplayAsync(context, reservation.Response!);
            return;
        }

        var originalBody = context.Response.Body;
        await using var responseBody = new MemoryStream();
        try
        {
            await coordinator.BeginExecutionAsync(context.RequestAborted);
            postCommitActions.BeginDeferral();
            context.Response.Body = responseBody;
            await next(context);
            responseBody.Position = 0;
            var response = await new StreamReader(responseBody, Encoding.UTF8, leaveOpen: true).ReadToEndAsync(context.RequestAborted);
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                await coordinator.CompleteAndCommitAsync(
                    reservation.Id,
                    new IdempotentResponse(
                        context.Response.StatusCode,
                        context.Response.ContentType ?? "application/json",
                        response,
                        context.Response.Headers.Location.FirstOrDefault(),
                        context.Response.Headers.ETag.FirstOrDefault(),
                        context.Response.Headers["X-Board-ETag"].FirstOrDefault()),
                    timeProvider.GetUtcNow(),
                    CancellationToken.None);
                await postCommitActions.DrainAsync(CancellationToken.None);
            }
            else
            {
                postCommitActions.Discard();
                await coordinator.AbortAsync(reservation.Id, CancellationToken.None);
            }
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBody, context.RequestAborted);
        }
        catch
        {
            postCommitActions.Discard();
            try
            {
                await coordinator.AbortAsync(reservation.Id, CancellationToken.None);
            }
            catch (Exception exception)
            {
                AbortFailed(logger, exception);
            }
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static string CanonicalOperation(HttpContext context)
    {
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
            ?? context.Request.Path.Value?.ToLowerInvariant()
            ?? string.Empty;
        return $"{context.Request.Method.ToUpperInvariant()}:{route.ToLowerInvariant()}";
    }

    private static string RequestHash(HttpContext context, string operation, ReadOnlySpan<byte> body)
    {
        var query = string.Join('&', context.Request.Query
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .SelectMany(item => item.Value.Order(StringComparer.Ordinal)
                .Select(value => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(value ?? string.Empty)}")));
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var metadata = Encoding.UTF8.GetBytes(
            $"{operation}\n{path}\n{query}\n{context.Request.ContentType?.ToLowerInvariant() ?? string.Empty}\n");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(metadata);
        hash.AppendData(body);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task ReplayAsync(HttpContext context, IdempotentResponse response)
    {
        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = response.ContentType;
        if (response.Location is not null) context.Response.Headers.Location = response.Location;
        if (response.Etag is not null) context.Response.Headers.ETag = response.Etag;
        if (response.BoardEtag is not null) context.Response.Headers["X-Board-ETag"] = response.BoardEtag;
        context.Response.Headers["Idempotency-Replayed"] = "true";
        await context.Response.WriteAsync(response.ResponseBody, context.RequestAborted);
    }
}
