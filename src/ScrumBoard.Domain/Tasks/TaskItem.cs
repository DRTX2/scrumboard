using ScrumBoard.Domain.Primitives;

namespace ScrumBoard.Domain.Tasks;

public sealed class TaskItem
{
    private TaskItem() { }

    public TaskItem(
        Guid id,
        Guid projectId,
        Guid columnId,
        string title,
        string? description,
        TaskPriority priority,
        Guid assigneeId,
        DateOnly? dueDate,
        long position,
        DateTimeOffset now)
    {
        Id = Guard.Required(id, nameof(id));
        ProjectId = Guard.Required(projectId, nameof(projectId));
        ColumnId = Guard.Required(columnId, nameof(columnId));
        Title = Guard.Required(title, nameof(title), 200);
        Description = Guard.Optional(description, 4_000);
        Priority = Guard.Defined(priority, nameof(priority));
        AssigneeId = Guard.Required(assigneeId, nameof(assigneeId));
        DueDate = dueDate;
        Position = Guard.Positive(position, nameof(position));
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ColumnId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskPriority Priority { get; private set; }
    public Guid AssigneeId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public long Position { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string title,
        string? description,
        TaskPriority priority,
        Guid assigneeId,
        DateOnly? dueDate,
        DateTimeOffset now)
    {
        Title = Guard.Required(title, nameof(title), 200);
        Description = Guard.Optional(description, 4_000);
        Priority = Guard.Defined(priority, nameof(priority));
        AssigneeId = Guard.Required(assigneeId, nameof(assigneeId));
        DueDate = dueDate;
        Version++;
        UpdatedAt = now;
    }

    public void Move(Guid columnId, long position, DateTimeOffset now)
    {
        ColumnId = Guard.Required(columnId, nameof(columnId));
        Position = Guard.Positive(position, nameof(position));
        Version++;
        UpdatedAt = now;
    }
}
