namespace ScrumBoard.Application.Ports.Outbound;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
