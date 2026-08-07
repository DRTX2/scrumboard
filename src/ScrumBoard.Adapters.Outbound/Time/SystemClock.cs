using ScrumBoard.Application.Ports.Out;

namespace ScrumBoard.Adapters.Outbound.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
