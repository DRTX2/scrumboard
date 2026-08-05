using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Application.Models.Tasks;

public sealed record TaskFilter(Guid? AssigneeId = null, TaskPriority? Priority = null, string? Search = null);
