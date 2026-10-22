using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;
// Shared transaction mechanics for the explicitly supported administrative resources.
internal sealed class InstitutionAdministrationPersistence(ElectronicRegisterContext db, IMembershipAuthorizationRepository authorization)
{
    private static readonly HashSet<string> Tables = ["study_area", "study_path", "biennium", "biennium_study_area", "biennium_study_path", "class_subject", "campus"];
    private static string Table(string table) => Tables.Contains(table) ? table : throw new ArgumentException("Risorsa non supportata.");
    public async Task LockAsync(TenantContext tenant, string table, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(tenant);
        await using var command = Command($"SELECT id FROM `{Table(table)}` WHERE institution_id=@tenant AND id=@id FOR UPDATE", tenant.InstitutionId, id);
        if (await command.ExecuteScalarAsync(ct)is null)
            throw InstitutionSchoolQueries.Missing();
    }

    private System.Data.Common.DbCommand Command(string sql, Guid? tenant = null, Guid? id = null)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Administrative mutation requires a transaction.");
        var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = sql;
        if (tenant.HasValue)
        {
            var p = command.CreateParameter();
            p.ParameterName = "@tenant";
            p.Value = tenant.Value;
            command.Parameters.Add(p);
        }

        if (id.HasValue)
        {
            var p = command.CreateParameter();
            p.ParameterName = "@id";
            p.Value = id.Value;
            command.Parameters.Add(p);
        }

        return command;
    }

    // Discover every actual incoming FK, including future CASCADE and SET NULL constraints.
    // The parent is already locked; locking child reads use the current committed state.
    public async Task NoDependentsAsync(TenantContext tenant, string table, Guid id, CancellationToken ct)
    {
        InstitutionSchoolQueries.Admin(tenant);
        Table(table);
        var references = new List<(string Table, string Column)>();
        await using (var unsupported = Command("SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME='" + table + "' AND REFERENCED_COLUMN_NAME NOT IN ('id','institution_id')"))
            if (Convert.ToInt64(await unsupported.ExecuteScalarAsync(ct)) != 0)
                throw new BusinessRuleException("Inventario dipendenze da aggiornare.");
        await using (var inventory = Command("SELECT DISTINCT TABLE_NAME,COLUMN_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME='" + table + "' AND REFERENCED_COLUMN_NAME='id'"))
        await using (var reader = await inventory.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                references.Add((reader.GetString(0), reader.GetString(1)));
        foreach (var reference in references)
        {
            static string Quote(string value) => "`" + value.Replace("`", "``") + "`";
            // Scope even metadata-discovered children through the explicitly owned parent.
            // This also catches a future unowned child table without permitting its cascade.
            await using var check = Command($"SELECT 1 FROM {Quote(reference.Table)} c JOIN `{table}` p ON c.{Quote(reference.Column)}=p.id WHERE p.institution_id=@tenant AND p.id=@id LIMIT 1 FOR UPDATE", tenant.InstitutionId, id);
            if (await check.ExecuteScalarAsync(ct)is not null)
                throw new BusinessRuleException("La risorsa contiene dipendenze didattiche o amministrative.");
        }
    }

    public Task EventAsync(TenantContext t, string type, Guid id, long revision, string action, string? before, string? after, CancellationToken ct)
    {
        Table(type);
        return db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO institution_administration_event(institution_id,id,actor_user_id,actor_membership_id,entity_type,entity_id,revision,action,before_json,after_json,occurred_at_utc) VALUES ({t.InstitutionId},{Guid.NewGuid()},{t.UserId},{t.MembershipId},{type},{id},{revision},{action},{before},{after},UTC_TIMESTAMP(6))", ct);
    }

    public async Task WriteAsync(TenantContext actor, Func<Task> write)
    {
        try
        {
            // Tenant-first authorization read locks block IAM revocation until commit,
            // while remaining compatible with owner/actor FK checks in existing workflows.
            // Resource writes use exclusive row locks; unique ledger revisions also reject
            // a stale REPEATABLE READ snapshot and roll the entire mutation back.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={actor.InstitutionId} LOCK IN SHARE MODE");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution_membership WHERE institution_id={actor.InstitutionId} AND id={actor.MembershipId} LOCK IN SHARE MODE");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT r.role FROM institution_membership_role r JOIN institution_membership m ON m.id=r.membership_id WHERE m.institution_id={actor.InstitutionId} AND m.id={actor.MembershipId} ORDER BY r.role LOCK IN SHARE MODE");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT membership_id FROM institution_membership_security_state WHERE institution_id={actor.InstitutionId} AND membership_id={actor.MembershipId} LOCK IN SHARE MODE");
            var current = await authorization.GetAsync(actor.UserId, actor.InstitutionId, actor.MembershipId);
            if (current is null || current.Status != MembershipStatus.Active || current.InstitutionStatus != InstitutionStatus.Active || current.SecurityGeneration is null || current.SecurityGeneration != actor.SecurityGeneration || !actor.Roles.SetEquals(current.Roles) || !current.Roles.Contains(InstitutionRole.InstitutionAdmin))
                throw new UnauthorizedAccessException("Amministratore istituzionale non autorizzato.");
            await write();
        }
        catch (DbUpdateException e)when (e.InnerException is MySqlException { Number: 1062 or 1451 or 1452 or 1644 or 1213 or 1205 })
        {
            throw new BusinessRuleException("Risorsa duplicata, modificata o utilizzata.");
        }
        catch (MySqlException e)when (e.Number is 1062 or 1451 or 1452 or 1644 or 1213 or 1205)
        {
            throw new BusinessRuleException("Risorsa duplicata, modificata o utilizzata.");
        }
    }
}
