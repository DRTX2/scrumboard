using ClosedXML.Excel;
using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Reporting;

internal sealed class ExcelReportExporter : IReportExporter
{
    private const int ColumnCount = 6;
    private const int HeaderRow = 10;
    private const string DateFormat = "dd/mm/yyyy";
    private const string DateTimeFormat = "dd/mm/yyyy hh:mm";

    public string Format => "xlsx";
    public string MediaType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => "xlsx";

    public byte[] Export(ProjectReportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Informe del proyecto");
        sheet.Cell(1, 1).Value = ReportPresentation.Title;
        sheet.Range(1, 1, 1, ColumnCount).Merge();
        sheet.Cell(1, 1).Style
            .Font.SetBold()
            .Font.SetFontSize(18)
            .Font.SetFontColor(XLColor.FromHtml("#1E3A8A"));

        SetMetadata(sheet, 2, "Proyecto", data.ProjectName);
        SetMetadata(sheet, 3, ReportPresentation.DescriptionLabel, ReportPresentation.Description(data));
        SetMetadata(sheet, 4, ReportPresentation.StatusLabel, ReportPresentation.Status(data.Status));
        SetMetadataDate(sheet, 5, ReportPresentation.StartDateLabel, data.StartDate);
        SetMetadataDate(sheet, 6, ReportPresentation.ExpectedEndDateLabel, data.ExpectedEndDate);
        SetMetadataDateTime(sheet, 7, ReportPresentation.GeneratedAtLabel, data.GeneratedAt.ToLocalTime().DateTime);

        sheet.Cell(8, 1).Value = ReportPresentation.EvaluatedPeriod(data);
        sheet.Range(8, 1, 8, ColumnCount).Merge();
        sheet.Cell(8, 1).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#EFF6FF"));

        for (var index = 0; index < ReportPresentation.TaskHeaders.Length; index++)
        {
            sheet.Cell(HeaderRow, index + 1).Value = ReportPresentation.TaskHeaders[index];
        }
        var header = sheet.Range(HeaderRow, 1, HeaderRow, ColumnCount);
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        header.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#1E3A8A"));

        for (var index = 0; index < data.Tasks.Count; index++)
        {
            var task = data.Tasks[index];
            var row = HeaderRow + index + 1;
            sheet.Cell(row, 1).Value = task.Title;
            sheet.Cell(row, 2).Value = task.Column;
            sheet.Cell(row, 3).Value = task.Assignee ?? ReportPresentation.MissingAssignee;
            sheet.Cell(row, 4).Value = ReportPresentation.Priority(task.Priority);
            SetDateTime(sheet.Cell(row, 5), task.CreatedAt.ToLocalTime().DateTime);
            if (task.DueDate is { } dueDate)
            {
                SetDate(sheet.Cell(row, 6), dueDate);
            }
            else
            {
                sheet.Cell(row, 6).Value = ReportPresentation.MissingDueDate;
            }
        }

        var lastDataRow = HeaderRow + data.Tasks.Count;
        sheet.Range(HeaderRow, 1, Math.Max(HeaderRow, lastDataRow), ColumnCount).SetAutoFilter();
        if (data.Tasks.Count == 0)
        {
            sheet.Cell(HeaderRow + 1, 1).Value = ReportPresentation.EmptyTasks;
            sheet.Range(HeaderRow + 1, 1, HeaderRow + 1, ColumnCount).Merge();
            sheet.Cell(HeaderRow + 1, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
        }

        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Column(1).Width = 34;
        sheet.Column(2).Width = 20;
        sheet.Column(3).Width = 24;
        sheet.Column(4).Width = 14;
        sheet.Column(5).Width = 19;
        sheet.Column(6).Width = 19;
        sheet.RangeUsed()!.Style.Alignment.WrapText = true;
        sheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.RowsUsed().AdjustToContents(15, 60);
        sheet.PageSetup.Header.Right.AddText("&KDDDDDD&9ScrumBoard", XLHFOccurrence.AllPages);
        sheet.PageSetup.Footer.Left.AddText("&KDDDDDD&9ScrumBoard", XLHFOccurrence.AllPages);
        sheet.PageSetup.Footer.Right.AddText("Página &P de &N", XLHFOccurrence.AllPages);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetMetadata(IXLWorksheet sheet, int row, string label, string value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 1).Style.Font.SetBold();
        sheet.Cell(row, 2).Value = value;
        sheet.Range(row, 2, row, ColumnCount).Merge();
    }

    private static void SetMetadataDate(IXLWorksheet sheet, int row, string label, DateOnly value)
    {
        SetMetadata(sheet, row, label, string.Empty);
        SetDate(sheet.Cell(row, 2), value);
    }

    private static void SetMetadataDateTime(IXLWorksheet sheet, int row, string label, DateTime value)
    {
        SetMetadata(sheet, row, label, string.Empty);
        SetDateTime(sheet.Cell(row, 2), value);
    }

    private static void SetDate(IXLCell cell, DateOnly value)
    {
        cell.Value = value.ToDateTime(TimeOnly.MinValue);
        cell.Style.NumberFormat.Format = DateFormat;
    }

    private static void SetDateTime(IXLCell cell, DateTime value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = DateTimeFormat;
    }
}
