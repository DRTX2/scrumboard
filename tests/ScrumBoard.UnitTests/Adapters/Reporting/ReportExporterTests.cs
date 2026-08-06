using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Infrastructure.Adapters.Outbound.Reporting;
using ScrumBoard.Infrastructure.Configuration;

namespace ScrumBoard.UnitTests.Adapters.Reporting;

public sealed class ReportExporterTests
{
    private const int HeaderRow = 10;

    static ReportExporterTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public void Presentation_UsesCompleteSpanishVocabularyAndHostLocalPeriod()
    {
        var data = CreateReport();

        Assert.Equal("Periodo evaluado: tareas históricas hasta hoy " +
                     $"({ReportDateFormatter.Format(ReportPresentation.LocalDate(data.GeneratedAt))})",
            ReportPresentation.EvaluatedPeriod(data));
        Assert.Equal(["Planificado", "Activo", "Completado", "Archivado"],
            Enum.GetValues<ProjectStatus>().Select(ReportPresentation.Status));
        Assert.Equal(["Baja", "Media", "Alta", "Crítica"],
            Enum.GetValues<TaskPriority>().Select(ReportPresentation.Priority));
        Assert.Equal(["Tarea", "Columna", "Responsable", "Prioridad", "Creada", "Vence"],
            ReportPresentation.TaskHeaders);
    }

    [Fact]
    public void Pdf_ProducesPdfSignatureForPopulatedAndEmptySpanishReports()
    {
        var exporter = new PdfReportExporter();

        var populated = exporter.Export(CreateReport());
        var empty = exporter.Export(CreateReport() with { Tasks = [] });

        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(populated, 0, 5));
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(empty, 0, 5));
        Assert.True(populated.Length > 1_000);
        Assert.True(empty.Length > 1_000);
    }

    [Fact]
    public void Excel_ContainsSpanishMetadataTasksRealDatesAndPrintWatermarks()
    {
        var data = CreateReport();
        var content = new ExcelReportExporter().Export(data);

        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("Informe del proyecto", sheet.Name);
        Assert.Equal(ReportPresentation.Title, sheet.Cell(1, 1).GetString());
        AssertMetadata(sheet, 2, "Proyecto", data.ProjectName);
        AssertMetadata(sheet, 3, ReportPresentation.DescriptionLabel, data.Description!);
        AssertMetadata(sheet, 4, ReportPresentation.StatusLabel, "Activo");
        Assert.Equal(ReportPresentation.StartDateLabel, sheet.Cell(5, 1).GetString());
        AssertDate(sheet.Cell(5, 2), data.StartDate.ToDateTime(TimeOnly.MinValue), "dd/mm/yyyy");
        Assert.Equal(ReportPresentation.ExpectedEndDateLabel, sheet.Cell(6, 1).GetString());
        AssertDate(sheet.Cell(6, 2), data.ExpectedEndDate.ToDateTime(TimeOnly.MinValue), "dd/mm/yyyy");
        Assert.Equal(ReportPresentation.GeneratedAtLabel, sheet.Cell(7, 1).GetString());
        AssertDate(sheet.Cell(7, 2), data.GeneratedAt.ToLocalTime().DateTime, "dd/mm/yyyy hh:mm");
        Assert.Equal(ReportPresentation.EvaluatedPeriod(data), sheet.Cell(8, 1).GetString());
        Assert.Equal(ReportPresentation.TaskHeaders,
            sheet.Row(HeaderRow).Cells(1, ReportPresentation.TaskHeaders.Length).Select(cell => cell.GetString()));

        Assert.Equal("Preparar lanzamiento", sheet.Cell(11, 1).GetString());
        Assert.Equal("En curso", sheet.Cell(11, 2).GetString());
        Assert.Equal("Ada Lovelace", sheet.Cell(11, 3).GetString());
        Assert.Equal("Alta", sheet.Cell(11, 4).GetString());
        AssertDate(sheet.Cell(11, 5), data.Tasks[0].CreatedAt.ToLocalTime().DateTime, "dd/mm/yyyy hh:mm");
        AssertDate(sheet.Cell(11, 6), data.Tasks[0].DueDate!.Value.ToDateTime(TimeOnly.MinValue), "dd/mm/yyyy");
        Assert.Equal(ReportPresentation.MissingAssignee, sheet.Cell(12, 3).GetString());
        Assert.Equal("Crítica", sheet.Cell(12, 4).GetString());
        Assert.Equal(ReportPresentation.MissingDueDate, sheet.Cell(12, 6).GetString());
        Assert.True(sheet.AutoFilter.IsEnabled);
        Assert.Equal(HeaderRow, sheet.SheetView.SplitRow);
        Assert.Contains("ScrumBoard", sheet.PageSetup.Header.Right.GetText(XLHFOccurrence.OddPages));
        Assert.Contains("ScrumBoard", sheet.PageSetup.Footer.Left.GetText(XLHFOccurrence.OddPages));
        Assert.Contains("Página", sheet.PageSetup.Footer.Right.GetText(XLHFOccurrence.OddPages));
        Assert.All(sheet.Columns(1, 6), column => Assert.True(column.Width >= 14));
    }

    [Fact]
    public void Excel_EmptyReportRetainsHeadersFiltersAndSpanishEmptyState()
    {
        var content = new ExcelReportExporter().Export(CreateReport() with { Description = null, Tasks = [] });

        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Informe del proyecto");

        Assert.Equal(ReportPresentation.MissingDescription, sheet.Cell(3, 2).GetString());
        Assert.Equal(ReportPresentation.TaskHeaders,
            sheet.Row(HeaderRow).Cells(1, ReportPresentation.TaskHeaders.Length).Select(cell => cell.GetString()));
        Assert.Equal(ReportPresentation.EmptyTasks, sheet.Cell(HeaderRow + 1, 1).GetString());
        Assert.True(sheet.AutoFilter.IsEnabled);
    }

    [Fact]
    public void DependencyInjection_ExposesCompleteExporterInventory()
    {
        var services = new ServiceCollection();
        services.AddReportExporters();
        using var provider = services.BuildServiceProvider();

        var exporters = provider.GetServices<IReportExporter>().ToArray();

        Assert.Equal(["pdf", "xlsx"], exporters.Select(exporter => exporter.Format));
        Assert.All(exporters, exporter => Assert.NotEmpty(exporter.MediaType));
        Assert.All(exporters, exporter => Assert.NotEmpty(exporter.FileExtension));
    }

    private static ProjectReportData CreateReport()
    {
        var generatedAt = new DateTimeOffset(2026, 8, 5, 18, 42, 0, TimeSpan.Zero);
        return new ProjectReportData(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            "Proyecto Alfa",
            "Preparación coordinada del lanzamiento.",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            ProjectStatus.Active,
            generatedAt,
            [
                new ProjectReportTask("Preparar lanzamiento", "En curso", "Ada Lovelace", TaskPriority.High,
                    generatedAt.AddDays(-2), new DateOnly(2026, 8, 12)),
                new ProjectReportTask("Validar riesgos", "Pendiente", null, TaskPriority.Critical,
                    generatedAt.AddDays(-1), null)
            ]);
    }

    private static void AssertMetadata(IXLWorksheet sheet, int row, string label, string value)
    {
        Assert.Equal(label, sheet.Cell(row, 1).GetString());
        Assert.Equal(value, sheet.Cell(row, 2).GetString());
    }

    private static void AssertDate(IXLCell cell, DateTime expected, string format)
    {
        Assert.Equal(XLDataType.DateTime, cell.DataType);
        Assert.Equal(expected, cell.GetDateTime());
        Assert.Equal(format, cell.Style.NumberFormat.Format);
    }
}
