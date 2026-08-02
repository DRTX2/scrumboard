using ScrumBoard.Domain.Common;

namespace ScrumBoard.Domain.Boards;

public sealed class BoardColumn
{
    private BoardColumn() { }

    public BoardColumn(Guid id, Guid projectId, string name, long position, DateTimeOffset now)
    {
        Id = id;
        ProjectId = projectId;
        Name = Guard.Required(name, nameof(name), 100);
        Position = position;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public long Position { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string name, DateTimeOffset now)
    {
        Name = Guard.Required(name, nameof(name), 100);
        Version++;
        UpdatedAt = now;
    }

    public void MoveTo(long position, DateTimeOffset now)
    {
        Position = position;
        Version++;
        UpdatedAt = now;
    }

    public static void EnsureCanDelete(bool containsTasks)
    {
        if (containsTasks)
        {
            throw new DomainException("column_not_empty", "A column containing tasks cannot be deleted.");
        }
    }
}
