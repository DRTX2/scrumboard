using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Context;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Reports;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Application.UseCases.Reports;

public sealed class ReportUseCase(
    IProjectRepository projects,
    IReportDataSource dataSource,
    IEnumerable<IReportExporter> exporters,
    ICurrentUser currentUser,
    IClock clock) : IReportUseCase
{
    public async Task<GeneratedReport> GenerateAsync(
        Guid projectId,
        string format,
        TaskFilter filter,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated ||
            !await projects.IsMemberAsync(projectId, currentUser.UserId, cancellationToken))
        {
            throw HiddenNotFound();
        }

        var availableExporters = exporters.ToList();
        var exporter = availableExporters.FirstOrDefault(item => item.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConflictException("unsupported_report_format",
                $"Supported report formats are {string.Join(" and ", availableExporters
                    .Select(item => item.Format)
                    .Distinct(StringComparer.OrdinalIgnoreCase))}.");
        var data = await dataSource.GetAsync(projectId, filter with { Search = filter.Search?.Trim() }, clock.UtcNow, cancellationToken)
            ?? throw new NotFoundException("project_not_found", "The project was not found.");
        var safeName = string.Concat(data.ProjectName.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        return new GeneratedReport(
            exporter.Export(data),
            exporter.MediaType,
            $"{safeName}-{data.GeneratedAt:yyyyMMdd-HHmm}.{exporter.FileExtension}");
    }

    private static NotFoundException HiddenNotFound() =>
        new("project_not_found", "The project was not found.");
}
