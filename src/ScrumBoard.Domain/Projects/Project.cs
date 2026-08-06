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
        Id = Guard.Required(id, nameof(id));
        Name = Guard.Required(name, nameof(name), 160);
        Description = Guard.Optional(description, 2_000);
        StartDate = startDate;
        ExpectedEndDate = expectedEndDate;
        Status = Guard.Defined(status, nameof(status));
        CreatedAt = now;
        UpdatedAt = now;
        _members.Add(new ProjectMember(id, Guard.Required(ownerId, nameof(ownerId)), ProjectRole.Owner));
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
        Status = Guard.Defined(status, nameof(status));
        TouchBoard(now);
    }

    public void AddMember(Guid userId, ProjectRole role, DateTimeOffset now)
    {
        if (_members.Any(member => member.UserId == userId))
        {
            throw new DomainException("member_exists", "El usuario ya pertenece al proyecto.");
        }

        _members.Add(new ProjectMember(Id, Guard.Required(userId, nameof(userId)), Guard.Defined(role, nameof(role))));
        Touch(now);
    }

    public void TouchBoard(DateTimeOffset now)
    {
        Version++;
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
        if (startDate == default || expectedEndDate == default)
        {
            throw new DomainException("project_dates_required", "Las fechas de inicio y fin previstas son obligatorias.");
        }

        if (expectedEndDate < startDate)
        {
            throw new DomainException("invalid_project_dates", "La fecha prevista de fin no puede ser anterior a la fecha de inicio.");
        }
    }
}
