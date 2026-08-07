using ScrumBoard.Application.Errors;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Tasks;
using ScrumBoard.Application.Ports.Inbound.Reports;
using ScrumBoard.Application.Ports.Out;

namespace ScrumBoard.Application.UseCases.Reports;

public sealed class ReportUseCase(
    IReportDataSource dataSource,
    IEnumerable<IReportExporter> exporters,
    ICurrentUser currentUser,
    IClock clock) : IReportUseCase
{
    public const int MaximumSynchronousTaskRows = 10_000;

    public async Task<GeneratedReport> GenerateAsync(
        Guid projectId,
        string format,
        TaskFilter filter,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated) throw HiddenNotFound();

        var availableExporters = exporters.ToList();
        var exporter = availableExporters.FirstOrDefault(item =>
            item.Format.Equals(format, StringComparison.OrdinalIgnoreCase));
        if (exporter is null)
        {
            throw new ConflictException("unsupported_report_format",
                $"Los formatos de reporte admitidos son {string.Join(" y ", availableExporters
                    .Select(item => item.Format)
                    .Distinct(StringComparer.OrdinalIgnoreCase))}.");
        }

        var data = await dataSource.GetAsync(projectId, currentUser.UserId,
                filter with { Search = filter.Search?.Trim() }, clock.UtcNow,
                MaximumSynchronousTaskRows + 1, cancellationToken)
            ?? throw HiddenNotFound();
        if (data.Tasks.Count > MaximumSynchronousTaskRows)
        {
            throw new ValidationException("report_too_large",
                "El reporte no puede exportarse de forma síncrona porque supera el límite de 10.000 tareas.");
        }

        var safeName = string.Concat(data.ProjectName
                .Select(character => char.IsLetterOrDigit(character) ? character : '-'))
            .Trim('-');
        if (safeName.Length > 100) safeName = safeName[..100].TrimEnd('-');
        if (string.IsNullOrEmpty(safeName)) safeName = "reporte-proyecto";
        return new GeneratedReport(
            exporter.Export(data),
            exporter.MediaType,
            $"{safeName}-{data.GeneratedAt.UtcDateTime:yyyyMMdd-HHmm}.{exporter.FileExtension}");
    }

    private static NotFoundException HiddenNotFound() =>
        new("project_not_found", "No se encontró el proyecto.");
}
