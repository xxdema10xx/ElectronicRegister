using System.Data.Common;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectronicRegisterAPI.Infrastructure.Services;

// Global email mutex first (the same row the institutional assignment and profile-link
// triggers lock, always after the tenant row), then one bulk read of every reserved
// address sharing a candidate prefix. No address is reserved here: the INSERT of the
// User and its reservation trigger are the uniqueness authority.
internal sealed class PlatformEmailAllocator(ElectronicRegisterContext db, PlatformIdentitySettings settings) : IPlatformEmailAllocator
{
    private const int PrefixesPerQuery = 100;

    public async Task<PlatformEmailPlan> PlanAsync(IReadOnlyCollection<string> localBases, CancellationToken ct)
    {
        settings.RequireEnabled();
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Transazione richiesta.");
        await db.Database.ExecuteSqlRawAsync("SELECT id FROM institution_email_mutex WHERE id=1 FOR UPDATE", ct);
        var reserved = new List<string>();
        var prefixes = localBases.Select(PlatformEmailRules.LookupPrefix).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var chunk in prefixes.Chunk(PrefixesPerQuery))
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
            command.CommandText = "SELECT address FROM institution_email_reservation WHERE " + string.Join(" OR ", chunk.Select((_, i) => "address LIKE @p" + i));
            for (var i = 0; i < chunk.Length; i++)
                Add(command, "@p" + i, chunk[i] + "%@" + settings.EmailDomain);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                reserved.Add(reader.GetString(0));
        }
        return new PlatformEmailPlan(settings.EmailDomain, reserved);
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
