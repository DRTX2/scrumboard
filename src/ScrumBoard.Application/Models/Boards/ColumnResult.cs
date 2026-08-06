namespace ScrumBoard.Application.Models.Boards;

public sealed record ColumnResult(
    Guid Id,
    Guid ProjectId,
    string Name,
    long Position,
    long Version,
    long BoardVersion);
