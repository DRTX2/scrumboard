using ScrumBoard.Application.Reports;

namespace ScrumBoard.Application.Abstractions;

public interface IReportExporter
{
    string Format { get; }
    string ContentType { get; }
    string FileExtension { get; }
    byte[] Export(ProjectReportData data);
}
