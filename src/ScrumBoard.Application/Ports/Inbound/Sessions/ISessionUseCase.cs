namespace ScrumBoard.Application.Ports.Inbound.Sessions;

public interface ISessionUseCase
{
    Task<SessionResponse> CreateAsync(CreateSession request, CancellationToken cancellationToken);
}
