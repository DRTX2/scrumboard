namespace ScrumBoard.Domain.Idempotency;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { }

    public IdempotencyRecord(
        Guid id,
        Guid userId,
        string operation,
        string key,
        string requestHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        Operation = operation;
        Key = key;
        RequestHash = requestHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string? ContentType { get; private set; }
    public string? ResponseBody { get; private set; }
    public string? Location { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsCompleted => CompletedAt is not null;

    public void Complete(int statusCode, string contentType, string responseBody, string? location, DateTimeOffset now)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        ResponseBody = responseBody;
        Location = location;
        CompletedAt = now;
    }
}
