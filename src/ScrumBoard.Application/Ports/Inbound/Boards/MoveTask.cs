namespace ScrumBoard.Application.Ports.Inbound.Boards;

public sealed record MoveTask(Guid ColumnId, Guid? BeforeTaskId, Guid? AfterTaskId);
