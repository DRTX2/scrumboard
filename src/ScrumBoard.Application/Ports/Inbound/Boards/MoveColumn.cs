namespace ScrumBoard.Application.Ports.Inbound.Boards;

public sealed record MoveColumn(Guid? BeforeColumnId, Guid? AfterColumnId);
