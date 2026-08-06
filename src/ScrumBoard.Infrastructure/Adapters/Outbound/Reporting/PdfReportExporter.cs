using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Reporting;

internal sealed class PdfReportExporter : IReportExporter
{
    public string Format => "pdf";
    public string MediaType => "application/pdf";
    public string FileExtension => "pdf";

    public byte[] Export(ProjectReportData data) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken4));
            page.Background()
                .AlignMiddle()
                .AlignCenter()
                .Rotate(-28)
                .Text("ScrumBoard")
                .FontSize(76)
                .SemiBold()
                .FontColor("#F3F4F6");
            page.Header().Row(row =>
            {
                row.RelativeItem().Text("ScrumBoard").SemiBold().FontColor(Colors.Blue.Darken2);
                row.ConstantItem(220).AlignRight().Text(ReportPresentation.Title).FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(10);
                column.Item().Text(data.ProjectName)
                    .FontSize(21)
                    .SemiBold()
                    .FontColor(Colors.Blue.Darken2);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(110);
                        columns.RelativeColumn();
                    });
                    MetadataRow(table, ReportPresentation.DescriptionLabel, ReportPresentation.Description(data));
                    MetadataRow(table, ReportPresentation.StatusLabel, ReportPresentation.Status(data.Status));
                    MetadataRow(table, ReportPresentation.StartDateLabel, ReportDateFormatter.Format(data.StartDate));
                    MetadataRow(table, ReportPresentation.ExpectedEndDateLabel,
                        ReportDateFormatter.Format(data.ExpectedEndDate));
                    MetadataRow(table, ReportPresentation.GeneratedAtLabel, ReportDateFormatter.Format(data.GeneratedAt));
                });
                column.Item()
                    .Background(Colors.Blue.Lighten5)
                    .BorderLeft(3)
                    .BorderColor(Colors.Blue.Darken1)
                    .Padding(8)
                    .Text(ReportPresentation.EvaluatedPeriod(data))
                    .SemiBold();

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.25f);
                        columns.RelativeColumn(1.65f);
                        columns.RelativeColumn(1.65f);
                    });
                    table.Header(header =>
                    {
                        foreach (var label in ReportPresentation.TaskHeaders)
                        {
                            header.Cell()
                                .Background(Colors.Blue.Darken2)
                                .Padding(6)
                                .Text(label)
                                .FontColor(Colors.White)
                            .SemiBold();
                        }
                    });
                    if (data.Tasks.Count == 0)
                    {
                        table.Cell()
                            .ColumnSpan((uint)ReportPresentation.TaskHeaders.Length)
                            .BorderBottom(1)
                            .BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.Grey.Lighten4)
                            .Padding(18)
                            .AlignCenter()
                            .Text(ReportPresentation.EmptyTasks)
                            .Italic()
                            .FontColor(Colors.Grey.Darken1);
                    }

                    foreach (var task in data.Tasks)
                    {
                        TaskCell(table, task.Title);
                        TaskCell(table, task.Column);
                        TaskCell(table, task.Assignee ?? ReportPresentation.MissingAssignee);
                        TaskCell(table, ReportPresentation.Priority(task.Priority));
                        TaskCell(table, ReportDateFormatter.Format(task.CreatedAt));
                        TaskCell(table, task.DueDate is { } dueDate
                            ? ReportDateFormatter.Format(dueDate)
                            : ReportPresentation.MissingDueDate);
                    }
                });
            });
            page.Footer().DefaultTextStyle(style => style.FontColor(Colors.Grey.Darken1)).AlignCenter().Text(text =>
            {
                text.Span("Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });
    }).GeneratePdf();

    private static void MetadataRow(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(2).Text($"{label}:").SemiBold();
        table.Cell().PaddingVertical(2).Text(value);
    }

    private static void TaskCell(TableDescriptor table, string value) =>
        table.Cell()
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(5)
            .Text(value);
}
