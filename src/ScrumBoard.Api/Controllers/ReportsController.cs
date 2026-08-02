using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Application.Boards;
using ScrumBoard.Application.Reports;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/reports")]
public sealed class ReportsController(ReportService reports) : ControllerBase
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
        var report = await reports.GenerateAsync(projectId, format, new BoardFilter(assigneeId, priority, search), cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }
}
