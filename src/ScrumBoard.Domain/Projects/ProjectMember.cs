using ScrumBoard.Domain.Primitives;

namespace ScrumBoard.Domain.Projects;

public sealed class ProjectMember
{
    private ProjectMember() { }

    public ProjectMember(Guid projectId, Guid userId, ProjectRole role)
    {
        ProjectId = Guard.Required(projectId, nameof(projectId));
        UserId = Guard.Required(userId, nameof(userId));
        Role = Guard.Defined(role, nameof(role));
    }

    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectRole Role { get; private set; }
}
