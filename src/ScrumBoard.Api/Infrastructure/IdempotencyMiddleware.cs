using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Common;
using ScrumBoard.Domain.Idempotency;
using ScrumBoard.Infrastructure.Persistence;

namespace ScrumBoard.Api.Infrastructure;

internal sealed class IdempotencyMiddleware(RequestDelegate next)
{
    private const string HeaderName = "Idempotency-Key";

    public async Task InvokeAsync(
        HttpContext context,
        ScrumBoardDbContext dbContext,
        ICurrentUser currentUser,
        IClock clock)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !currentUser.IsAuthenticated ||
            !context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            await next(context);
            return;
        }

        var key = values.ToString().Trim();
        if (key.Length is 0 or > 100) throw new BadHttpRequestException("Idempotency-Key must contain between 1 and 100 characters.");
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, false, leaveOpen: true);
        var body = await reader.ReadToEndAsync(context.RequestAborted);
        context.Request.Body.Position = 0;
        var operation = $"{context.Request.Method}:{context.Request.Path}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var now = clock.UtcNow;
        var existing = await dbContext.IdempotencyRecords.SingleOrDefaultAsync(record =>
            record.UserId == currentUser.UserId && record.Operation == operation && record.Key == key,
            context.RequestAborted);
        if (existing is not null && existing.ExpiresAt <= now)
        {
            dbContext.IdempotencyRecords.Remove(existing);
            await dbContext.SaveChangesAsync(context.RequestAborted);
            existing = null;
        }
        if (existing is not null)
        {
            await ReplayAsync(context, existing, hash);
            return;
        }

        var record = new IdempotencyRecord(Guid.NewGuid(), currentUser.UserId, operation, key, hash,
            now, now.AddHours(24));
        dbContext.IdempotencyRecords.Add(record);
        try
        {
            await dbContext.SaveChangesAsync(context.RequestAborted);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(record).State = EntityState.Detached;
            var concurrent = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(item =>
                item.UserId == currentUser.UserId && item.Operation == operation && item.Key == key,
                context.RequestAborted);
            if (concurrent is null) throw;
            await ReplayAsync(context, concurrent, hash);
            return;
        }
        var originalBody = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        try
        {
            await next(context);
            responseBody.Position = 0;
            var response = await new StreamReader(responseBody, Encoding.UTF8, leaveOpen: true).ReadToEndAsync(context.RequestAborted);
            if (context.Response.StatusCode is >= 200 and < 300)
            {
                record.Complete(context.Response.StatusCode, context.Response.ContentType ?? "application/json", response,
                    context.Response.Headers.Location.FirstOrDefault(), clock.UtcNow);
            }
            else
            {
                dbContext.IdempotencyRecords.Remove(record);
            }
            await dbContext.SaveChangesAsync(context.RequestAborted);
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBody, context.RequestAborted);
        }
        catch
        {
            dbContext.IdempotencyRecords.Remove(record);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static async Task ReplayAsync(HttpContext context, IdempotencyRecord record, string requestHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(record.RequestHash), Convert.FromHexString(requestHash)))
            throw new ConflictException("idempotency_key_reused", "The idempotency key was already used with another request.");
        if (!record.IsCompleted)
            throw new ConflictException("request_in_progress", "A request with this idempotency key is still being processed.");
        context.Response.StatusCode = record.StatusCode;
        context.Response.ContentType = record.ContentType;
        if (record.Location is not null) context.Response.Headers.Location = record.Location;
        context.Response.Headers["Idempotency-Replayed"] = "true";
        await context.Response.WriteAsync(record.ResponseBody ?? string.Empty, context.RequestAborted);
    }
}
