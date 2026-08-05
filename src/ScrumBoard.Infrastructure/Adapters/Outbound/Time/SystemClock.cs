using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
