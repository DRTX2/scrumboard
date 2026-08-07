using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Ports.Inbound.Projects;

public sealed record UpdateProject(
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status);
