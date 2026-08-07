using ScrumBoard.Application.Models.Common;
using ScrumBoard.Application.Models.Projects;

namespace ScrumBoard.Application.Ports.Inbound.Projects;

public interface IProjectUseCase
{
    Task<PagedResult<ProjectSummary>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken);
    Task<ProjectDetails> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ProjectDetails> CreateAsync(CreateProject request, CancellationToken cancellationToken);
    Task<ProjectDetails> UpdateAsync(Guid id, UpdateProject request, long expectedVersion, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken);
}
