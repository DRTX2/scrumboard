using ScrumBoard.Application.Boards;
using ScrumBoard.Application.Reports;

namespace ScrumBoard.Application.Abstractions;

public interface IReportDataSource
{
    Task<ProjectReportData?> GetAsync(
        Guid projectId,
        Guid userId,
        BoardFilter filter,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken);
}
