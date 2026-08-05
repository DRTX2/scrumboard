using Microsoft.EntityFrameworkCore;
using ScrumBoard.Application.Models.Common;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Repositories;

internal sealed class ProjectRepository(ScrumBoardDbContext dbContext) : IProjectRepository
{
    public async Task<PagedResult<ProjectSummary>> ListAsync(
        Guid userId,
        ProjectSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var projects =
            from project in dbContext.Projects.AsNoTracking()
            join membership in dbContext.ProjectMembers.AsNoTracking() on project.Id equals membership.ProjectId
            where membership.UserId == userId
            select new { project, membership.Role };

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var pattern = PostgreSqlLike.ContainsLiteral(criteria.Search);
            projects = projects.Where(item => EF.Functions.ILike(
                item.project.Name, pattern, PostgreSqlLike.EscapeCharacter));
        }

        var total = await projects.LongCountAsync(cancellationToken);
        var ascending = criteria.Direction is SortDirection.Ascending;
        projects = criteria.Sort switch
        {
            ProjectSortField.Name => ascending ? projects.OrderBy(item => item.project.Name) : projects.OrderByDescending(item => item.project.Name),
            ProjectSortField.StartDate => ascending ? projects.OrderBy(item => item.project.StartDate) : projects.OrderByDescending(item => item.project.StartDate),
            ProjectSortField.Status => ascending ? projects.OrderBy(item => item.project.Status) : projects.OrderByDescending(item => item.project.Status),
            _ => ascending ? projects.OrderBy(item => item.project.UpdatedAt) : projects.OrderByDescending(item => item.project.UpdatedAt)
        };

        var items = await projects
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(item => new ProjectSummary(
                item.project.Id,
                item.project.Name,
                item.project.Description,
                item.project.StartDate,
                item.project.ExpectedEndDate,
                item.project.Status,
                item.Role,
                item.project.Version,
                item.project.UpdatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<ProjectSummary>(items, criteria.Page, criteria.PageSize, total);
    }

    public Task<Project?> FindAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.Include(project => project.Members)
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

    public Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AsNoTracking()
            .AnyAsync(member => member.ProjectId == projectId && member.UserId == userId, cancellationToken);

    public Task<ProjectDetails?> GetDetailsAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        (from project in dbContext.Projects.AsNoTracking()
         join membership in dbContext.ProjectMembers.AsNoTracking() on project.Id equals membership.ProjectId
         where project.Id == projectId && membership.UserId == userId
         select new ProjectDetails(
             project.Id, project.Name, project.Description, project.StartDate, project.ExpectedEndDate,
             project.Status, membership.Role, project.Version, project.BoardVersion, project.CreatedAt, project.UpdatedAt))
        .SingleOrDefaultAsync(cancellationToken);

    public void Add(Project project) => dbContext.Projects.Add(project);
    public void Remove(Project project) => dbContext.Projects.Remove(project);
}
