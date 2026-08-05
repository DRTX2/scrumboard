using ScrumBoard.Domain.Primitives;

namespace ScrumBoard.Domain.Projects;

public sealed class Project
{
    private readonly List<ProjectMember> _members = [];
    private Project() { }

    public Project(
        Guid id,
        string name,
        string? description,
        DateOnly startDate,
        DateOnly expectedEndDate,
        ProjectStatus status,
        Guid ownerId,
        DateTimeOffset now)
    {
        EnsureDates(startDate, expectedEndDate);
        Id = id;
        Name = Guard.Required(name, nameof(name), 160);
        Description = Guard.Optional(description, 2_000);
        StartDate = startDate;
        ExpectedEndDate = expectedEndDate;
        Status = status;
        CreatedAt = now;
        UpdatedAt = now;
        _members.Add(new ProjectMember(id, ownerId, ProjectRole.Owner));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly ExpectedEndDate { get; private set; }
    public ProjectStatus Status { get; private set; }
    public long Version { get; private set; } = 1;
    public long BoardVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    public void Update(
        string name,
        string? description,
        DateOnly startDate,
        DateOnly expectedEndDate,
        ProjectStatus status,
        DateTimeOffset now)
    {
        EnsureDates(startDate, expectedEndDate);
        Name = Guard.Required(name, nameof(name), 160);
        Description = Guard.Optional(description, 2_000);
        StartDate = startDate;
        ExpectedEndDate = expectedEndDate;
        Status = status;
        Touch(now);
    }

    public void AddMember(Guid userId, ProjectRole role, DateTimeOffset now)
    {
        if (_members.Any(member => member.UserId == userId))
        {
            throw new DomainException("member_exists", "The user is already a project member.");
        }

        _members.Add(new ProjectMember(Id, userId, role));
        Touch(now);
    }

    public void TouchBoard(DateTimeOffset now)
    {
        BoardVersion++;
        UpdatedAt = now;
    }

    private void Touch(DateTimeOffset now)
    {
        Version++;
        UpdatedAt = now;
    }

    private static void EnsureDates(DateOnly startDate, DateOnly expectedEndDate)
    {
        if (expectedEndDate < startDate)
        {
            throw new DomainException("invalid_project_dates", "Expected end date cannot be before start date.");
        }
    }
}
