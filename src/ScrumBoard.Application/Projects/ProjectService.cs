using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Common;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Projects;

public sealed class ProjectService(
    IProjectRepository projects,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public Task<PagedResult<ProjectSummary>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken) =>
        projects.ListAsync(RequireCurrentUser(), Normalize(query), cancellationToken);

    public async Task<ProjectDetails> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await projects.GetDetailsAsync(id, RequireCurrentUser(), cancellationToken)
        ?? throw HiddenNotFound();

    public async Task<ProjectDetails> CreateAsync(CreateProject request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var project = new Project(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            request.StartDate,
            request.ExpectedEndDate,
            request.Status,
            userId,
            clock.UtcNow);

        projects.Add(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await projects.GetDetailsAsync(project.Id, userId, cancellationToken)
            ?? throw new InvalidOperationException("Created project could not be read.");
    }

    public async Task<ProjectDetails> UpdateAsync(
        Guid id,
        UpdateProject request,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        EnsureVersion(project.Version, expectedVersion);
        project.Update(
            request.Name,
            request.Description,
            request.StartDate,
            request.ExpectedEndDate,
            request.Status,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        EnsureVersion(project.Version, expectedVersion);
        projects.Remove(project);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Project> GetOwnedProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await projects.FindAsync(id, cancellationToken);
        var membership = project?.Members.SingleOrDefault(member => member.UserId == RequireCurrentUser());
        if (project is null || membership is null)
        {
            throw HiddenNotFound();
        }

        if (membership.Role is not ProjectRole.Owner)
        {
            throw new ForbiddenException("project_owner_required", "Project owner permission is required.");
        }

        return project;
    }

    private Guid RequireCurrentUser() => currentUser.IsAuthenticated
        ? currentUser.UserId
        : throw new AuthenticationFailedException();

    private static ProjectListQuery Normalize(ProjectListQuery query) => query with
    {
        Page = Math.Max(1, query.Page),
        PageSize = Math.Clamp(query.PageSize, 1, 100),
        Search = query.Search?.Trim(),
        Sort = query.Sort.Trim(),
        Direction = query.Direction.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc"
    };

    private static void EnsureVersion(long actual, long expected)
    {
        if (actual != expected)
        {
            throw new PreconditionFailedException("etag_mismatch", "The resource changed after it was read.");
        }
    }

    private static NotFoundException HiddenNotFound() =>
        new("project_not_found", "The project was not found.");
}
