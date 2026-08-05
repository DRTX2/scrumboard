using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Tasks;

namespace ScrumBoard.Application.Ports.Outbound;

public interface IReportDataSource
{
    Task<ProjectReportData?> GetAsync(
        Guid projectId,
        TaskFilter filter,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken);
}
