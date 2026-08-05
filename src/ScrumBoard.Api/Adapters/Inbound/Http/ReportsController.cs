using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Reports;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Adapters.Inbound.Http;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/reports")]
public sealed class ReportsController(IReportUseCase reports) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Generate(
        Guid projectId,
        [FromQuery] string format,
        [FromQuery] Guid? assigneeId,
        [FromQuery] TaskPriority? priority,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var report = await reports.GenerateAsync(projectId, format, new TaskFilter(assigneeId, priority, search), cancellationToken);
        return File(report.Content, report.MediaType, report.FileName);
    }
}
