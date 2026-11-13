using System.Data.Common;
using System.Globalization;
using System.Text;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Persistence.Import;
using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

// E4: platform Student accounts of one import batch, inside the batch transaction (READ
// COMMITTED, tenant row and IAM locks already held). Global email mutex, bulk fiscal-code
// lookup under lock, in-memory address plan in row order, multi-row INSERTs in chunks and
// per-row fallback only for a chunk that hit an address taken outside the mutex.
internal sealed class PlatformImportAccounts(
    ElectronicRegisterContext db,
    TenantContext actor,
    IPlatformEmailAllocator allocator,
    PlatformLifecycleWriter lifecycle,
    IMailboxProvisioner mailbox,
    CancellationToken ct
)
{
    private const int Chunk = 200;

    private DbCommand Command(string sql, IReadOnlyList<(string Name, object? Value)> values)
    {
        var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = (db.Database.CurrentTransaction ?? throw new InvalidOperationException("Transazione richiesta.")).GetDbTransaction();
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private async Task ExecuteAsync(string sql, IReadOnlyList<(string Name, object? Value)> values)
    {
        await using var command = Command(sql, values);
        await command.ExecuteNonQueryAsync(ct);
    }

    // Multi-row INSERT of `rows` tuples, each rendered as (@p{row}_{column}, ...).
    private async Task InsertAsync<T>(string head, IReadOnlyList<T> rows, Func<T, object?[]> values)
    {
        foreach (var chunk in rows.Chunk(Chunk))
        {
            var sql = new StringBuilder(head).Append(" VALUES ");
            var parameters = new List<(string, object?)>();
            for (var r = 0; r < chunk.Length; r++)
            {
                var row = values(chunk[r]);
                sql.Append(r == 0 ? "(" : ",(");
                for (var c = 0; c < row.Length; c++)
                {
                    if (row[c] is RawSql raw)
                        sql.Append(c == 0 ? "" : ",").Append(raw.Text);
                    else
                    {
                        var name = "@p" + r.ToString(CultureInfo.InvariantCulture) + "_" + c.ToString(CultureInfo.InvariantCulture);
                        sql.Append(c == 0 ? "" : ",").Append(name);
                        parameters.Add((name, row[c]));
                    }
                }
                sql.Append(')');
            }
            await ExecuteAsync(sql.ToString(), parameters);
        }
    }

    private sealed record RawSql(string Text);

    private static readonly RawSql Now = new("UTC_TIMESTAMP(6)");

    private async Task<List<object[]>> RowsAsync(string sqlHead, IReadOnlyList<object> keys, string tail = "")
    {
        var result = new List<object[]>();
        foreach (var chunk in keys.Chunk(Chunk))
        {
            var names = chunk.Select((_, i) => "@k" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
            await using var command = Command(sqlHead + "(" + string.Join(",", names) + ")" + tail, names.Zip(chunk, (n, k) => (n, (object?)k)).Append(("@t", (object?)actor.InstitutionId)).ToArray());
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                result.Add(row);
            }
        }
        return result;
    }

    public async Task AddItemsAsync(
        Guid batch,
        ImportCategory category,
        IReadOnlyList<ImportCreatedRow> rows,
        IReadOnlyList<string?> externalReferences
    )
    {
        if (rows.Count != externalReferences.Count)
            throw new ArgumentException("Righe e riferimenti non allineati.");
        var all = Enumerable.Range(0, rows.Count).ToArray();
        await InsertAsync("INSERT INTO institution_import_external_reference(institution_id,category,reference,student_id,teacher_id)", all.Where(i => externalReferences[i] is not null).ToArray(),
            i => [actor.InstitutionId, category.ToString(), externalReferences[i], rows[i].StudentId, rows[i].TeacherId]);
        await InsertAsync("INSERT INTO institution_import_item(institution_id,batch_id,`row_number`,category,student_id,teacher_id,email_assignment_id,external_reference)", all,
            i => [actor.InstitutionId, batch, rows[i].RowNumber, category.ToString(), rows[i].StudentId, rows[i].TeacherId, rows[i].EmailAssignmentId, externalReferences[i]]);
    }

    public async Task<IReadOnlySet<string>> MembersAsync(IReadOnlyCollection<string> hmacs)
    {
        if (hmacs.Count == 0)
            return new HashSet<string>();
        if (!await lifecycle.InstalledAsync(ct))
            throw new InvalidOperationException("Schema di identità di piattaforma assente.");
        var rows = await RowsAsync("SELECT u.fiscal_code_hmac FROM `user` u JOIN institution_membership m ON m.user_id=u.id AND m.institution_id=@t WHERE u.fiscal_code_hmac IN ", hmacs.Cast<object>().ToArray());
        return rows.Select(r => (string)r[0]).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> EmailsAsync(IReadOnlyCollection<Guid> users)
    {
        if (users.Count == 0)
            return new Dictionary<Guid, string>();
        // Only accounts that are members of this institution: nothing of another tenant.
        var rows = await RowsAsync("SELECT u.id,u.email FROM `user` u WHERE EXISTS(SELECT 1 FROM institution_membership m WHERE m.user_id=u.id AND m.institution_id=@t) AND u.id IN ", users.Cast<object>().ToArray());
        return rows.ToDictionary(r => (Guid)r[0], r => (string)r[1]);
    }

    public async Task<IReadOnlyList<ImportCreatedAccount>> CreateAsync(IReadOnlyList<ImportAccountRequest> people)
    {
        if (!await lifecycle.InstalledAsync(ct))
            throw new InvalidOperationException("Schema di identità di piattaforma assente.");
        var plan = await allocator.PlanAsync(people.Select(p => p.LocalBase).Distinct(StringComparer.Ordinal).ToArray(), ct);
        // Existing people, locked (the global mutex serializes every platform creation).
        var found = (await RowsAsync("SELECT id,fiscal_code_hmac,lifecycle_state,email FROM `user` WHERE fiscal_code_hmac IN ", people.Select(p => (object)p.FiscalCode.Hmac).ToArray(), " FOR UPDATE"))
            .ToDictionary(r => (string)r[1], r => (Id: (Guid)r[0], Closed: (string)r[2] == nameof(AccountLifecycleState.Closed), Email: (string)r[3]), StringComparer.Ordinal);
        if (found.Count > 0 && (await RowsAsync("SELECT user_id FROM institution_membership WHERE institution_id=@t AND user_id IN ", found.Values.Select(f => (object)f.Id).ToArray())).Count > 0)
            throw new ImportConflictException("FiscalCodeExistsInInstitution");
        var accounts = new (Guid User, string Email, string? Password)[people.Count];
        var fresh = new List<int>();
        for (var i = 0; i < people.Count; i++)
        {
            var person = people[i];
            if (found.TryGetValue(person.FiscalCode.Hmac, out var existing))
            {
                if (existing.Closed && !await lifecycle.ReactivateAsync(existing.Id, LifecycleRuleCodes.AccountRecreated, person.PasswordHash, ct))
                    throw new ImportConflictException("DatabaseConflict");
                // D-A: an active person keeps its credential; a reactivated one gets a new one.
                accounts[i] = (existing.Id, existing.Email, existing.Closed ? person.TemporaryPassword : null);
            }
            else
            {
                accounts[i] = (Guid.NewGuid(), plan.Take(person.LocalBase), person.TemporaryPassword);
                fresh.Add(i);
            }
        }
        await InsertUsersAsync(people, accounts, fresh, plan);
        await InsertAsync("INSERT INTO identity_account_state(user_id,generation)", fresh, i => [accounts[i].User, 0]);
        await InsertAsync("INSERT INTO identity_account_event(id,user_id,action)", fresh.SelectMany(i => new[] { (i, IdentityAccountEvents.Provisioned), (i, IdentityAccountEvents.TemporaryPassword) }).ToArray(),
            e => [Guid.NewGuid(), accounts[e.Item1].User, e.Item2]);
        var memberships = people.Select(_ => Guid.NewGuid()).ToArray();
        var studentIds = people.Select(_ => Guid.NewGuid()).ToArray();
        var all = Enumerable.Range(0, people.Count).ToArray();
        await InsertAsync("INSERT INTO institution_membership(id,institution_id,user_id,status,joined_at)", all, i => [memberships[i], actor.InstitutionId, accounts[i].User, "active", Now]);
        await InsertAsync("INSERT INTO institution_membership_role(membership_id,role)", all, i => [memberships[i], nameof(InstitutionRole.Student)]);
        // Initial classes were resolved in this tenant by the preview; re-check them in bulk
        // under the batch lock. The composite FK (class_institution_id, class_id) set by
        // mt3a_student_insert also rejects a class of another tenant.
        var classes = people.Where(p => p.ClassId.HasValue).Select(p => (object)p.ClassId!.Value).Distinct().ToArray();
        if (classes.Length > 0 && (await RowsAsync("SELECT id FROM class WHERE institution_id=@t AND id IN ", classes)).Count != classes.Length)
            throw new ImportConflictException("PreviewContextChanged");
        await InsertAsync("INSERT INTO student(id,first_name,last_name,class_id,institution_id)", all, i => [studentIds[i], people[i].FirstName, people[i].LastName, people[i].ClassId, actor.InstitutionId]);
        await InsertAsync("INSERT INTO institution_student_profile(institution_id,membership_id,student_id)", all, i => [actor.InstitutionId, memberships[i], studentIds[i]]);
        foreach (var i in all.Where(i => accounts[i].Password is not null))
            await mailbox.CreateAsync(accounts[i].User, accounts[i].Email, ct);
        return all.Select(i => new ImportCreatedAccount(studentIds[i], new ImportAccountRow(people[i].RowNumber, accounts[i].User, memberships[i], accounts[i].Email, accounts[i].Password))).ToArray();
    }

    // New users in row order. A chunk that fails with 1062 on an address (a writer outside the
    // mutex) is retried row by row with the next candidate; InnoDB rolled the chunk back whole.
    private async Task InsertUsersAsync(
        IReadOnlyList<ImportAccountRequest> people,
        (Guid User, string Email, string? Password)[] accounts,
        IReadOnlyList<int> fresh,
        PlatformEmailPlan plan
    )
    {
        const string head = "INSERT INTO `user`(id,email,password_hash,role,fiscal_code_hmac,fiscal_code_hmac_key_id,fiscal_code_enc,fiscal_code_enc_key_id)";
        object?[] Values(int i) => [accounts[i].User, accounts[i].Email, people[i].PasswordHash, "student", people[i].FiscalCode.Hmac, people[i].FiscalCode.HmacKeyId, people[i].FiscalCode.Cipher, people[i].FiscalCode.CipherKeyId];
        foreach (var chunk in fresh.Chunk(Chunk))
        {
            try
            {
                await InsertAsync(head, chunk, Values);
                continue;
            }
            catch (MySqlException e) when (e.Number == 1062 && !e.Message.Contains("uq_user_fiscal_code_hmac", StringComparison.Ordinal))
            {
            }
            foreach (var i in chunk)
                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        await InsertAsync(head, [i], Values);
                        break;
                    }
                    catch (MySqlException e) when (e.Number == 1062 && !e.Message.Contains("uq_user_fiscal_code_hmac", StringComparison.Ordinal) && attempt < 50)
                    {
                        plan.MarkTaken(accounts[i].Email);
                        accounts[i] = accounts[i] with { Email = plan.Take(people[i].LocalBase) };
                    }
                }
        }
    }
}
