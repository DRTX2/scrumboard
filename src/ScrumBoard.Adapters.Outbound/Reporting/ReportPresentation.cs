using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Adapters.Outbound.Reporting;

internal static class ReportPresentation
{
    public const string Title = "Informe del proyecto";
    public const string EmptyTasks = "No hay tareas para los filtros seleccionados.";
    public const string MissingAssignee = "Sin asignar";
    public const string MissingDescription = "Sin descripción";
    public const string MissingDueDate = "Sin fecha límite";
    public const string DescriptionLabel = "Descripción";
    public const string StatusLabel = "Estado";
    public const string StartDateLabel = "Fecha de inicio";
    public const string ExpectedEndDateLabel = "Fecha prevista de finalización";
    public const string GeneratedAtLabel = "Fecha de generación";

    public static readonly string[] TaskHeaders =
        ["Tarea", "Columna", "Responsable", "Prioridad", "Creada", "Vence"];

    public static string Description(ProjectReportData data) =>
        string.IsNullOrWhiteSpace(data.Description) ? MissingDescription : data.Description;

    public static string EvaluatedPeriod(ProjectReportData data) =>
        $"Periodo evaluado: tareas históricas hasta hoy ({ReportDateFormatter.Format(LocalDate(data.GeneratedAt))})";

    public static string Status(ProjectStatus status) => status switch
    {
        ProjectStatus.Planned => "Planificado",
        ProjectStatus.Active => "Activo",
        ProjectStatus.Completed => "Completado",
        ProjectStatus.Archived => "Archivado",
        _ => status.ToString()
    };

    public static string Priority(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => "Baja",
        TaskPriority.Medium => "Media",
        TaskPriority.High => "Alta",
        TaskPriority.Critical => "Crítica",
        _ => priority.ToString()
    };

    public static DateOnly LocalDate(DateTimeOffset value) => DateOnly.FromDateTime(value.ToLocalTime().DateTime);
}
