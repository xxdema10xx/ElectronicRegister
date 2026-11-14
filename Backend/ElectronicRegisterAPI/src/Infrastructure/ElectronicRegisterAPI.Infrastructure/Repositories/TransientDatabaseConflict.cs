using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

// Account creation takes the tenant lock and then the global email mutex; flows that reach
// the mutex through profile triggers in a different order can lose a deadlock (A21). InnoDB
// rolls the victim back as a whole and every creation is idempotent by EventId, so the whole
// unit of work is repeated a bounded number of times after a short random wait. When the
// attempts run out the caller gets PlatformBusyException, never a conflict answer.
internal static class TransientDatabaseConflict
{
    internal const int Attempts = 4;
    private const int MinimumDelayMs = 20;
    private const int MaximumDelayMs = 120;

    internal static bool Is(Exception error) => error is MySqlException { Number: 1213 or 1205 } or DbUpdateException { InnerException: MySqlException { Number: 1213 or 1205 } };

    internal static async Task<T> RunAsync<T>(ElectronicRegisterContext db, Func<Task<T>> unitOfWork, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await unitOfWork();
            }
            catch (Exception error) when (Is(error) && !ct.IsCancellationRequested)
            {
                if (attempt >= Attempts)
                    throw new PlatformBusyException();
                // Nothing of the failed attempt may leak into the next one.
                db.ChangeTracker.Clear();
                await Task.Delay(Random.Shared.Next(MinimumDelayMs, MaximumDelayMs * attempt), ct);
            }
        }
    }
}
