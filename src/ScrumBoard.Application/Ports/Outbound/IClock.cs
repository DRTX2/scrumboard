namespace ScrumBoard.Application.Ports.Outbound;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
