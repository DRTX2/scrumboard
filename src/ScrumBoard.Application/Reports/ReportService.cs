using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Boards;
using ScrumBoard.Application.Common;

namespace ScrumBoard.Application.Reports;

public sealed class ReportService(
    IReportDataSource dataSource,
    IEnumerable<IReportExporter> exporters,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<GeneratedReport> GenerateAsync(
        Guid projectId,
        string format,
        BoardFilter filter,
        CancellationToken cancellationToken)
    {
        var exporter = exporters.SingleOrDefault(item => item.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConflictException("unsupported_report_format", "Supported report formats are pdf and xlsx.");
        var data = await dataSource.GetAsync(projectId, currentUser.UserId, filter, clock.UtcNow, cancellationToken)
            ?? throw new NotFoundException("project_not_found", "The project was not found.");
        var safeName = string.Concat(data.ProjectName.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        return new GeneratedReport(
            exporter.Export(data),
            exporter.ContentType,
            $"{safeName}-{data.GeneratedAt:yyyyMMdd-HHmm}.{exporter.FileExtension}");
    }
}
