using ScrumBoard.Application.Models.Reports;

namespace ScrumBoard.Application.Ports.Outbound;

public interface IReportExporter
{
    string Format { get; }
    string MediaType { get; }
    string FileExtension { get; }
    byte[] Export(ProjectReportData data);
}
