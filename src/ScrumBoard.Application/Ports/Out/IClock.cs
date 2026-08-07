namespace ScrumBoard.Application.Ports.Out;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
