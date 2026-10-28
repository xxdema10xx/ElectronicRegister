using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

public class TransactionManager : ITransactionManager
{
    private readonly ElectronicRegisterContext _context;
    public TransactionManager(ElectronicRegisterContext context)
    {
        _context = context;
    }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await action();
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Cancellation must not cancel cleanup of a transaction that already wrote rows.
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
