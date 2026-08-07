using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Adapters.Inbound.Http.Contracts;
using ScrumBoard.Adapters.Inbound.Infrastructure;
using ScrumBoard.Application.Ports.Inbound.Projects;

namespace ScrumBoard.Adapters.Inbound.Http;

[ApiController]
[Authorize]
[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectUseCase projects) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageResponse<ProjectResponse>>> List(
        [FromQuery] ProjectListRequest query,
        CancellationToken cancellationToken)
    {
        var page = await projects.ListAsync(query.ToQuery(), cancellationToken);
        Response.Headers["X-Total-Count"] = page.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Ok(new PageResponse<ProjectResponse>(
            page.Items.Select(ApiResponseMappings.ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount, page.TotalPages));
    }

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectDetailsResponse>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        EntityTags.Write(Response, project.Version);
        return Ok(project.ToResponse());
    }

    [HttpPost]
    [Idempotent]
    public async Task<ActionResult<ProjectDetailsResponse>> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await projects.CreateAsync(request.ToCommand(), cancellationToken);
        EntityTags.Write(Response, project.Version);
        return CreatedAtAction(nameof(Get), new { projectId = project.Id }, project.ToResponse());
    }

    [HttpPut("{projectId:guid}")]
    public async Task<ActionResult<ProjectDetailsResponse>> Update(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await projects.UpdateAsync(projectId, request.ToCommand(), EntityTags.Require(Request), cancellationToken);
        EntityTags.Write(Response, project.Version);
        return Ok(project.ToResponse());
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken cancellationToken)
    {
        await projects.DeleteAsync(projectId, EntityTags.Require(Request), cancellationToken);
        return NoContent();
    }
}
