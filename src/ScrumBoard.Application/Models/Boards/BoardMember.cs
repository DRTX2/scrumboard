using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Application.Models.Boards;

public sealed record BoardMember(Guid UserId, string Name, ProjectRole Role);
