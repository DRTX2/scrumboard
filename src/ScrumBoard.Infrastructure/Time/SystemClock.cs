using ScrumBoard.Application.Abstractions;

namespace ScrumBoard.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
