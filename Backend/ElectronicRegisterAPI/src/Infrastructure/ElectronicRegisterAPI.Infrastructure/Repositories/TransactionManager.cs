using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
            // Once the complete action has succeeded, finish its atomic commit.
            // Request cancellation must not turn that final boundary into an
            // ambiguous partial client outcome or cancel cleanup.
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch
        {
            // Cancellation must not cancel cleanup of a transaction that already wrote rows.
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (InvalidOperationException) when (_context.Database.GetDbConnection().State is System.Data.ConnectionState.Closed or System.Data.ConnectionState.Broken)
            {
                // A provider hard-timeout has already broken/closed this session.
                // Preserve the original timeout instead of replacing it with a
                // secondary "connection not open" error from rollback.
            }
            throw;
        }
    }
}
