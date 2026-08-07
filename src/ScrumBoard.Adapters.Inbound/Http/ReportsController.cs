using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumBoard.Adapters.Inbound.Http.Contracts;
using ScrumBoard.Application.Ports.Inbound.Reports;

namespace ScrumBoard.Adapters.Inbound.Http;

[ApiController]
[Authorize]
[Route("api/v1/projects/{projectId:guid}/reports")]
public sealed class ReportsController(IReportUseCase reports) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Generate(
        Guid projectId,
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken)
    {
        var report = await reports.GenerateAsync(projectId, query.Format, query.ToFilter(), cancellationToken);
        return File(report.Content, report.MediaType, report.FileName);
    }
}
