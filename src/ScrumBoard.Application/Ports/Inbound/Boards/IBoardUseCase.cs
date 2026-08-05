using ScrumBoard.Application.Models.Boards;
using ScrumBoard.Application.Models.Tasks;

namespace ScrumBoard.Application.Ports.Inbound.Boards;

public interface IBoardUseCase
{
    Task<BoardSnapshot> GetAsync(Guid projectId, TaskFilter filter, CancellationToken cancellationToken);
    Task<ColumnResult> CreateColumnAsync(Guid projectId, CreateColumn request, CancellationToken cancellationToken);
    Task<ColumnResult> UpdateColumnAsync(Guid projectId, Guid columnId, UpdateColumn request, long expectedVersion, CancellationToken cancellationToken);
    Task<ColumnResult> MoveColumnAsync(Guid projectId, Guid columnId, MoveColumn request, long expectedBoardVersion, CancellationToken cancellationToken);
    Task DeleteColumnAsync(Guid projectId, Guid columnId, long expectedVersion, CancellationToken cancellationToken);
    Task<TaskResult> CreateTaskAsync(Guid projectId, CreateTask request, CancellationToken cancellationToken);
    Task<TaskResult> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateTask request, long expectedVersion, CancellationToken cancellationToken);
    Task<TaskResult> MoveTaskAsync(Guid projectId, Guid taskId, MoveTask request, long expectedBoardVersion, CancellationToken cancellationToken);
    Task DeleteTaskAsync(Guid projectId, Guid taskId, long expectedVersion, CancellationToken cancellationToken);
    Task<IReadOnlyList<BoardMember>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken);
}
