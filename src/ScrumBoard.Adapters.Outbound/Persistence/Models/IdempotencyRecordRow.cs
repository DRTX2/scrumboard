namespace ScrumBoard.Adapters.Outbound.Persistence.Models;

public sealed class IdempotencyRecordRow
{
    private IdempotencyRecordRow() { }

    public IdempotencyRecordRow(
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
    public string? Etag { get; private set; }
    public string? BoardEtag { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsCompleted => CompletedAt is not null;

    public void Complete(
        int statusCode,
        string contentType,
        string responseBody,
        string? location,
        string? etag,
        string? boardEtag,
        DateTimeOffset completedAt)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        ResponseBody = responseBody;
        Location = location;
        Etag = etag;
        BoardEtag = boardEtag;
        CompletedAt = completedAt;
        ExpiresAt = completedAt.AddHours(24);
    }
}
