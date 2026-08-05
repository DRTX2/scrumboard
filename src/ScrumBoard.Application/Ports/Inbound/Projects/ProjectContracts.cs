using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Ports.Inbound.Projects;

public sealed record ProjectListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string Sort = "updatedAt",
    string Direction = "desc");

public sealed record CreateProject(
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status);

public sealed record UpdateProject(
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status);
