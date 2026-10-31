using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectronicRegisterAPI.Infrastructure.Services;

// Trusted, expiry-only cleanup. No web endpoint; no account/anagraphic/audit deletion.
internal sealed class InstitutionImportRetention(ElectronicRegisterContext db) : IInstitutionImportRetention
{
    private async Task<DbCommand> Command(string sql, params (string, object)[] values)
    {
        if (db.Database.GetDbConnection().State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync();
        var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var(name, value)in values)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        return cmd;
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken ct)
    {
        // Cleanup remains active when ingestion is rolled back/disabled. Old,
        // unmigrated databases are read-only detected and receive no import SQL.
        await using (var exists = await Command("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_import_batch'"))
            if (Convert.ToInt64(await exists.ExecuteScalarAsync(ct)) == 0)
                return 0;
        var candidates = new List<(string Tenant, string Id)>();
        await using (var cmd = await Command("SELECT institution_id,id FROM institution_import_batch WHERE state='Preview' AND expires_at_utc<=UTC_TIMESTAMP(6) ORDER BY institution_id,id LIMIT 20"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
                candidates.Add((Convert.ToString(r.GetValue(0))!, Convert.ToString(r.GetValue(1))!));
        }

        var count = 0;
        foreach (var(tenant, id)in candidates)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await using (var cmd = await Command("SELECT id FROM institution WHERE id=@t FOR UPDATE", ("@t", tenant)))
                await cmd.ExecuteNonQueryAsync(ct);
            long? revision = null;
            await using (var cmd = await Command("SELECT revision FROM institution_import_batch WHERE institution_id=@t AND id=@id AND state='Preview' AND expires_at_utc<=UTC_TIMESTAMP(6) FOR UPDATE", ("@t", tenant), ("@id", id)))
            {
                var value = await cmd.ExecuteScalarAsync(ct);
                if (value is not null && value is not DBNull)
                    revision = Convert.ToInt64(value);
            }

            if (revision is not null)
            {
                await using (var cmd = await Command("UPDATE institution_import_batch SET payload=NULL,state='Expired',revision=revision+1 WHERE institution_id=@t AND id=@id", ("@t", tenant), ("@id", id)))
                    await cmd.ExecuteNonQueryAsync(ct);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id + ":" + revision + ":Expired")));
                await using (var cmd = await Command("INSERT INTO institution_import_event(institution_id,id,batch_id,revision,action,actor_kind,request_hash,result_json,reason,occurred_at_utc) VALUES(@t,@event,@id,@rev,'Expired','Retention',@hash,JSON_OBJECT('expired',true),'Scadenza staging; dati personali rimossi',UTC_TIMESTAMP(6))", ("@t", tenant), ("@event", Guid.NewGuid().ToString("D")), ("@id", id), ("@rev", revision.Value + 1), ("@hash", hash)))
                    await cmd.ExecuteNonQueryAsync(ct);
                count++;
            }

            await tx.CommitAsync(ct);
        }

        return count;
    }
}
