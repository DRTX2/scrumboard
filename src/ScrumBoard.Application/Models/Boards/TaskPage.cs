namespace ScrumBoard.Application.Models.Boards;

public sealed record TaskPage(IReadOnlyList<BoardTask> Items, long Total, bool HasMore, long BoardVersion);
