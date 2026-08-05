using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Api.Adapters.Inbound.Http.Contracts;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Ports.Inbound.Projects;

namespace ScrumBoard.Api.Adapters.Inbound.Http;

[ApiController]
[Authorize]
[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectUseCase projects) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PageResponse<ProjectResponse>>> List(
        [FromQuery] ProjectListQuery query,
        CancellationToken cancellationToken)
    {
        var page = await projects.ListAsync(query, cancellationToken);
        Response.Headers["X-Total-Count"] = page.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Ok(new PageResponse<ProjectResponse>(
            page.Items.Select(ApiResponses.ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount, page.TotalPages));
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
    public async Task<ActionResult<ProjectDetailsResponse>> Create(CreateProject request, CancellationToken cancellationToken)
    {
        var project = await projects.CreateAsync(request, cancellationToken);
        EntityTags.Write(Response, project.Version);
        return CreatedAtAction(nameof(Get), new { projectId = project.Id }, project.ToResponse());
    }

    [HttpPut("{projectId:guid}")]
    public async Task<ActionResult<ProjectDetailsResponse>> Update(
        Guid projectId,
        UpdateProject request,
        CancellationToken cancellationToken)
    {
        var project = await projects.UpdateAsync(projectId, request, EntityTags.Require(Request), cancellationToken);
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
