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
        CancellationToken cancellationToken);

    Task<List<BoardMember>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken);
    Task<List<BoardColumn>> GetColumnsAsync(Guid projectId, CancellationToken cancellationToken);
    Task<BoardColumn?> FindColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken);
    Task<bool> ColumnContainsTasksAsync(Guid columnId, CancellationToken cancellationToken);
    Task<List<TaskItem>> GetTasksAsync(Guid projectId, Guid columnId, Guid? excludedTaskId, CancellationToken cancellationToken);
    Task<TaskItem?> FindTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);
    void AddColumn(BoardColumn column);
    void RemoveColumn(BoardColumn column);
    void AddTask(TaskItem task);
    void RemoveTask(TaskItem task);
}
