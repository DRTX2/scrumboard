using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Tasks;

namespace ScrumBoard.Application.Ports.Out;

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
