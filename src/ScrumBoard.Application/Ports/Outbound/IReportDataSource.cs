using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Tasks;

namespace ScrumBoard.Application.Ports.Outbound;

public interface IReportDataSource
{
    Task<ProjectReportData?> GetAsync(
        Guid projectId,
        Guid userId,
        TaskFilter filter,
        DateTimeOffset generatedAt,
        int taskRowLimit,
        CancellationToken cancellationToken);
}
