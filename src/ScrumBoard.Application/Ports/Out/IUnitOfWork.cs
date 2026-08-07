namespace ScrumBoard.Application.Ports.Out;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
