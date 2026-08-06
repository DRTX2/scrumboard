namespace ScrumBoard.Application.Models.Boards;

public sealed record BoardSnapshot(
    Guid ProjectId,
    string ProjectName,
    long BoardVersion,
    IReadOnlyList<BoardMember> Members,
    IReadOnlyList<BoardColumnItem> Columns);
