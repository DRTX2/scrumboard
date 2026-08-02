using ScrumBoard.Application.Common;
using ScrumBoard.Application.Projects;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Abstractions;

public interface IProjectRepository
{
    Task<PagedResult<ProjectSummary>> ListAsync(
        Guid userId,
        ProjectListQuery query,
        CancellationToken cancellationToken);

    Task<Project?> FindAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ProjectDetails?> GetDetailsAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
    void Add(Project project);
    void Remove(Project project);
}
