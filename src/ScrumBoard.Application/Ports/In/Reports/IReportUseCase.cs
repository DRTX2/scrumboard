using ScrumBoard.Application.Models.Reports;
using ScrumBoard.Application.Models.Tasks;

namespace ScrumBoard.Application.Ports.Inbound.Reports;

public interface IReportUseCase
{
    Task<GeneratedReport> GenerateAsync(Guid projectId, string format, TaskFilter filter, CancellationToken cancellationToken);
}
