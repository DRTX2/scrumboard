using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Ports.Outbound;

public interface IBoardRepository
{
    Task<BoardSnapshot?> GetSnapshotAsync(
        Guid projectId,
        Guid userId,
        TaskFilter filter,
        int taskLimit,
        CancellationToken cancellationToken);

    Task<TaskPageReadResult?> GetTaskPageAsync(
        Guid projectId,
        Guid columnId,
        Guid userId,
        TaskFilter filter,
        int limit,
        long? afterPosition,
        Guid? afterTaskId,
        long expectedBoardVersion,
        CancellationToken cancellationToken);

    Task<List<BoardMember>?> GetMembersAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
    Task<List<BoardColumn>> GetColumnsAsync(Guid projectId, CancellationToken cancellationToken);
    Task<BoardColumn?> FindColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken);
    Task<bool> ColumnContainsTasksAsync(Guid columnId, CancellationToken cancellationToken);
    Task<long?> GetMaxTaskPositionAsync(Guid projectId, Guid columnId, Guid? excludedTaskId, CancellationToken cancellationToken);
    Task<TaskOrderNeighbors?> GetTaskOrderNeighborsAsync(
        Guid projectId,
        Guid columnId,
        Guid excludedTaskId,
        Guid? beforeTaskId,
        Guid? afterTaskId,
        CancellationToken cancellationToken);
    Task<List<TaskItem>> GetTasksAsync(Guid projectId, Guid columnId, Guid? excludedTaskId, CancellationToken cancellationToken);
    Task<TaskItem?> FindTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);
    void AddColumn(BoardColumn column);
    void RemoveColumn(BoardColumn column);
    void AddTask(TaskItem task);
    void RemoveTask(TaskItem task);
}
