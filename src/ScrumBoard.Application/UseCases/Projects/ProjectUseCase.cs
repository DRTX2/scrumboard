using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Context;
using ScrumBoard.Application.Models.Common;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Ports.Inbound.Projects;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.UseCases.Projects;

public sealed class ProjectUseCase(
    IProjectRepository projects,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork) : IProjectUseCase
{
    public Task<PagedResult<ProjectSummary>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken)
    {
        query = InputValidation.Required(query, "request_required", "Los parámetros de consulta son obligatorios.");
        return projects.ListAsync(RequireCurrentUser(), ToCriteria(query), cancellationToken);
    }

    public async Task<ProjectDetails> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        InputValidation.Identifier(id, nameof(id));
        return await projects.GetDetailsAsync(id, RequireCurrentUser(), cancellationToken)
            ?? throw HiddenNotFound();
    }

    public async Task<ProjectDetails> CreateAsync(CreateProject request, CancellationToken cancellationToken)
    {
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        ValidateProject(request.Name, request.Description, request.StartDate, request.ExpectedEndDate);
        InputValidation.Defined<ProjectStatus>(request.Status, nameof(request.Status));
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
        request = InputValidation.Required(request, "request_required", "El cuerpo de la solicitud es obligatorio.");
        InputValidation.Identifier(id, nameof(id));
        InputValidation.Positive(expectedVersion, nameof(expectedVersion));
        ValidateProject(request.Name, request.Description, request.StartDate, request.ExpectedEndDate);
        InputValidation.Defined<ProjectStatus>(request.Status, nameof(request.Status));
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
        InputValidation.Identifier(id, nameof(id));
        InputValidation.Positive(expectedVersion, nameof(expectedVersion));
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        EnsureVersion(project.Version, expectedVersion);
        await projects.RemoveAsync(project, cancellationToken);
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
            throw new ForbiddenException("project_owner_required", "Se requiere el permiso de propietario del proyecto.");
        }

        return project;
    }

    private Guid RequireCurrentUser() => currentUser.IsAuthenticated
        ? currentUser.UserId
        : throw new AuthenticationFailedException();

    private static ProjectSearchCriteria ToCriteria(ProjectListQuery query)
    {
        InputValidation.Range(query.Page, 1, 100_000, nameof(query.Page));
        InputValidation.Range(query.PageSize, 1, 100, nameof(query.PageSize));
        var sort = (query.Sort ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "name" => ProjectSortField.Name,
            "startdate" => ProjectSortField.StartDate,
            "status" => ProjectSortField.Status,
            "updatedat" => ProjectSortField.UpdatedAt,
            _ => throw new ValidationException("invalid_sort", "El campo de ordenación no es válido.")
        };
        var direction = (query.Direction ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "asc" => SortDirection.Ascending,
            "desc" => SortDirection.Descending,
            _ => throw new ValidationException("invalid_sort_direction", "La dirección de ordenación no es válida.")
        };
        return new ProjectSearchCriteria(query.Page, query.PageSize, InputValidation.Search(query.Search), sort, direction);
    }

    private static void EnsureVersion(long actual, long expected)
    {
        if (actual != expected)
        {
            throw new OptimisticConcurrencyException("version_mismatch", "El recurso cambió después de ser leído.");
        }
    }

    private static void ValidateProject(
        string? name,
        string? description,
        DateOnly startDate,
        DateOnly expectedEndDate)
    {
        InputValidation.RequiredText(name, 160, nameof(name));
        InputValidation.OptionalText(description, 2_000, nameof(description));
        InputValidation.ProjectDates(startDate, expectedEndDate);
    }

    private static NotFoundException HiddenNotFound() =>
        new("project_not_found", "No se encontró el proyecto.");
}
