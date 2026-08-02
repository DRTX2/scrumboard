using ClosedXML.Excel;
using ScrumBoard.Application.Abstractions;
using ScrumBoard.Application.Reports;

namespace ScrumBoard.Infrastructure.Reports;

internal sealed class ExcelReportExporter : IReportExporter
{
    public string Format => "xlsx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => "xlsx";

    public byte[] Export(ProjectReportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Project report");
        sheet.Cell("A1").Value = data.ProjectName;
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 18;
        sheet.Range("A1:D1").Merge();
        sheet.Cell("A2").Value = $"Generated at {data.GeneratedAt:yyyy-MM-dd HH:mm} UTC";
        sheet.Range("A2:D2").Merge();
        var headers = new[] { "Task", "Column", "Assignee", "Priority" };
        for (var index = 0; index < headers.Length; index++) sheet.Cell(4, index + 1).Value = headers[index];
        var header = sheet.Range(4, 1, 4, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
        header.Style.Font.FontColor = XLColor.White;
        for (var index = 0; index < data.Tasks.Count; index++)
        {
            var task = data.Tasks[index];
            var row = index + 5;
            sheet.Cell(row, 1).Value = task.Title;
            sheet.Cell(row, 2).Value = task.Column;
            sheet.Cell(row, 3).Value = task.Assignee ?? "Unassigned";
            sheet.Cell(row, 4).Value = task.Priority.ToString();
        }
        sheet.SheetView.FreezeRows(4);
        sheet.Columns().AdjustToContents(8, 60);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
