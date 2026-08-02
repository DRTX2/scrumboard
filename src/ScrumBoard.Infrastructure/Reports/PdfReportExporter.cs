using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Reports;

namespace ScrumBoard.Infrastructure.Reports;

internal sealed class PdfReportExporter : IReportExporter
{
    private static readonly string[] Headers = ["Task", "Column", "Assignee", "Priority"];

    public string Format => "pdf";
    public string ContentType => "application/pdf";
    public string FileExtension => "pdf";

    public byte[] Export(ProjectReportData data) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontSize(10));
            page.Header().Column(column =>
            {
                column.Item().Text(data.ProjectName).FontSize(22).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"Generated at {data.GeneratedAt:yyyy-MM-dd HH:mm} UTC | Status: {data.Status}")
                    .FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingVertical(20).Column(column =>
            {
                if (!string.IsNullOrWhiteSpace(data.Description)) column.Item().PaddingBottom(12).Text(data.Description);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });
                    table.Header(header =>
                    {
                        foreach (var label in Headers)
                        {
                            header.Cell().Background(Colors.Blue.Darken2).Padding(6).Text(label).FontColor(Colors.White).SemiBold();
                        }
                    });
                    foreach (var task in data.Tasks)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(task.Title);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(task.Column);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(task.Assignee ?? "Unassigned");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(task.Priority.ToString());
                    }
                });
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }).GeneratePdf();
}
