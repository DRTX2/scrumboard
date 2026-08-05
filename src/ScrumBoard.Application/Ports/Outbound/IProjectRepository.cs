using ScrumBoard.Application.Models.Common;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Ports.Outbound;

public interface IProjectRepository
{
    Task<PagedResult<ProjectSummary>> ListAsync(
        Guid userId,
        ProjectSearchCriteria criteria,
        CancellationToken cancellationToken);

    Task<Project?> FindAsync(Guid projectId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
    Task<ProjectDetails?> GetDetailsAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
    void Add(Project project);
    void Remove(Project project);
}
