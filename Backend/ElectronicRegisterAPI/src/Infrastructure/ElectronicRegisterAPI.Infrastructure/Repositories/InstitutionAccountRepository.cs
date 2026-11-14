using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

// One transaction: tenant locks, global email mutex, fiscal-code lookup under lock, then
// either a new User (generated address, identity state and events), the existing person
// (membership/roles/profiles only, password untouched) or the reactivation of a Closed
// person, followed by the active membership (security state by trigger), roles, profile
// and its link and the audited Member onboarding event. The temporary password is only
// ever returned to the creating request.
internal sealed class InstitutionAccountRepository(
    ElectronicRegisterContext db,
    IInstitutionWriteAuthorization authorization,
    IInstitutionStudentRepository students,
    IInstitutionTeacherRepository teachers,
    IOnboardingFaultObserver faults,
    IPlatformEmailAllocator allocator,
    PlatformLifecycleWriter lifecycle,
    IMailboxProvisioner mailbox
) : IInstitutionAccountRepository
{
    private const string Action = "AccountCreated";
    private const int InsertAttempts = 50;

    private async Task<DbCommand> Command(string sql, CancellationToken ct, params (string Name, object? Value)[] values)
    {
        await db.Database.OpenConnectionAsync(ct);
        var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
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

    private async Task Execute(string sql, CancellationToken ct, params (string, object?)[] values)
    {
        await using var command = await Command(sql, ct, values);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<object?> Scalar(string sql, CancellationToken ct, params (string, object?)[] values)
    {
        await using var command = await Command(sql, ct, values);
        return await command.ExecuteScalarAsync(ct);
    }

    // Canonical intent: the fiscal code enters only as its HMAC, the generated address never
    // (replays must be deterministic), and the plaintext fiscal code is never hashed.
    private static string Intent(TenantContext actor, CreateInstitutionAccount r, string fiscalCodeHmac) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        actor.InstitutionId, actor.UserId, actor.MembershipId, r.EventId, FiscalCode = fiscalCodeHmac,
        Roles = r.Roles.Select(x => x.ToString()).Order(StringComparer.Ordinal).ToArray(),
        r.StudentId, r.TeacherId, r.FirstName, r.LastName, r.ClassId, r.Reason
    }))));

    private sealed record Stored(
        Guid UserId,
        Guid MembershipId,
        string[] Roles,
        Guid? StudentId,
        Guid? TeacherId
    );

    public Task<InstitutionAccountCreated> CreateAsync(
        TenantContext actor,
        CreateInstitutionAccount r,
        ProtectedFiscalCode fiscalCode,
        string localBase,
        string passwordHash,
        string temporaryPassword,
        CancellationToken ct = default
    ) => TransientDatabaseConflict.RunAsync(db, () => CreateOnceAsync(actor, r, fiscalCode, localBase, passwordHash, temporaryPassword, ct), ct);

    private async Task<InstitutionAccountCreated> CreateOnceAsync(
        TenantContext actor,
        CreateInstitutionAccount r,
        ProtectedFiscalCode fiscalCode,
        string localBase,
        string passwordHash,
        string temporaryPassword,
        CancellationToken ct
    )
    {
        var hash = Intent(actor, r, fiscalCode.Hmac);
        // READ COMMITTED, like the import batch: the IAM share locks of LockExamAsync stay
        // record locks (no gap locks), so a creation holding the global email mutex never
        // waits on the role-table gap held by a creation of another tenant that is queued on
        // the mutex (deadlock observed under REPEATABLE READ). Every read after the mutex
        // also sees the latest committed reservations. Uniqueness remains the unique keys.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        // Exclusive tenant mutex first (the profile-link trigger also locks the institution),
        // then the actor's current membership, roles, state and identity generation.
        await authorization.LockExamAsync(actor, ct);
        InstitutionAccountRules.RequireAllowed(actor, r.Roles);
        if (!await lifecycle.InstalledAsync(ct))
            throw new InvalidOperationException("Schema di identità di piattaforma assente.");
        await using (var prior = await Command("SELECT institution_id,action,intent_hash,result_json FROM institution_onboarding_event WHERE id=@e", ct, ("@e", r.EventId)))
        await using (var reader = await prior.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetGuid(0) != actor.InstitutionId || reader.GetString(1) != Action || reader.GetString(2) != hash)
                    throw new MembershipConflictException();
                var stored = JsonSerializer.Deserialize<Stored>(reader.GetString(3))!;
                await reader.DisposeAsync();
                var address = (string)(await Scalar("SELECT email FROM `user` WHERE id=@u", ct, ("@u", stored.UserId)))!;
                await tx.CommitAsync(ct);
                // Replays never reveal the password again: a lost response is recovered by a reset.
                return new(r.EventId, stored.UserId, stored.MembershipId, address, stored.Roles, stored.StudentId, stored.TeacherId, true, null);
            }
        }

        // Global email mutex and reservation snapshot. Every platform creation takes it after
        // the tenant lock, so the same fiscal code created concurrently by two institutions
        // is serialized and the second one finds the first person.
        var plan = await allocator.PlanAsync([localBase], ct);
        Guid user;
        string email;
        string? password = temporaryPassword;
        await using (var lookup = await Command("SELECT id,lifecycle_state FROM `user` WHERE fiscal_code_hmac=@h FOR UPDATE", ct, ("@h", fiscalCode.Hmac)))
        await using (var found = await lookup.ExecuteReaderAsync(ct))
        {
            var exists = await found.ReadAsync(ct);
            user = exists ? found.GetGuid(0) : Guid.NewGuid();
            var closed = exists && found.GetString(1) == nameof(AccountLifecycleState.Closed);
            await found.DisposeAsync();
            if (exists)
            {
                // E1: the person already belongs to this institution (any status). The tenant
                // already sees its own members, so this neutral conflict discloses nothing.
                if (await Scalar("SELECT 1 FROM institution_membership WHERE institution_id=@i AND user_id=@u", ct, ("@i", actor.InstitutionId), ("@u", user)) is not null)
                    throw new MembershipConflictException();
                if (closed)
                {
                    if (!await lifecycle.ReactivateAsync(user, LifecycleRuleCodes.AccountRecreated, passwordHash, ct))
                        throw new MembershipConflictException();
                }
                else
                {
                    // D-A: never reset the credential of a person active elsewhere.
                    password = null;
                }
                email = (string)(await Scalar("SELECT email FROM `user` WHERE id=@u", ct, ("@u", user)))!;
            }
            else
            {
                email = await InsertUserAsync(user, plan, localBase, fiscalCode, passwordHash, ct);
            }
        }

        var membership = Guid.NewGuid();
        await Execute("INSERT INTO institution_membership(id,institution_id,user_id,status,joined_at) VALUES(@m,@i,@u,'active',UTC_TIMESTAMP(6))", ct, ("@m", membership), ("@i", actor.InstitutionId), ("@u", user));
        foreach (var role in r.Roles.Select(x => x.ToString()).Order(StringComparer.Ordinal))
            await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,@r)", ct, ("@m", membership), ("@r", role));

        Guid? student = null, teacher = null;
        if (r.Roles.Contains(InstitutionRole.Student))
        {
            student = r.StudentId ?? Guid.NewGuid();
            if (r.StudentId.HasValue) await RequireOwned("student", student.Value, r, actor, ct);
            else await students.AddAsync(actor, new Student { Id = student.Value, FirstName = r.FirstName, LastName = r.LastName, ClassId = r.ClassId }, null, ct);
            await Execute("INSERT INTO institution_student_profile(institution_id,membership_id,student_id) VALUES(@i,@m,@s)", ct, ("@i", actor.InstitutionId), ("@m", membership), ("@s", student));
        }
        if (r.Roles.Contains(InstitutionRole.Teacher))
        {
            teacher = r.TeacherId ?? Guid.NewGuid();
            if (r.TeacherId.HasValue) await RequireOwned("teacher", teacher.Value, r, actor, ct);
            else await teachers.AddAsync(actor, new Teacher { Id = teacher.Value, FirstName = r.FirstName, LastName = r.LastName }, null, ct);
            await Execute("INSERT INTO institution_teacher_profile(institution_id,membership_id,teacher_id) VALUES(@i,@m,@t)", ct, ("@i", actor.InstitutionId), ("@m", membership), ("@t", teacher));
        }

        var roles = r.Roles.Select(x => x.ToString()).Order(StringComparer.Ordinal).ToArray();
        var result = new Stored(user, membership, roles, student, teacher);
        await Execute("INSERT INTO institution_onboarding_event(institution_id,id,actor_kind,actor_user_id,actor_membership_id,action,intent_hash,reason,result_json,occurred_at_utc) VALUES(@i,@e,'Member',@au,@am,@a,@h,@r,@j,UTC_TIMESTAMP(6))", ct,
            ("@i", actor.InstitutionId), ("@e", r.EventId), ("@au", actor.UserId), ("@am", actor.MembershipId), ("@a", Action), ("@h", hash), ("@r", r.Reason), ("@j", JsonSerializer.Serialize(result)));
        if (password is not null)
            await mailbox.CreateAsync(user, email, ct);
        await faults.BeforeCommitAsync("AccountCreate", ct);
        await tx.CommitAsync(ct);
        return new(r.EventId, user, membership, email, roles, student, teacher, false, password);
    }

    // New platform identity. The address comes from the plan read under the mutex; a 1062 on
    // the address (a writer outside the mutex) moves to the next candidate in the same
    // transaction: InnoDB rolls back only the failed statement, its triggers included.
    private async Task<string> InsertUserAsync(
        Guid user,
        PlatformEmailPlan plan,
        string localBase,
        ProtectedFiscalCode fiscalCode,
        string passwordHash,
        CancellationToken ct
    )
    {
        for (var attempt = 0; attempt < InsertAttempts; attempt++)
        {
            var email = plan.Take(localBase);
            try
            {
                // Historical required column, never an authorization decision.
                await Execute("INSERT INTO `user`(id,email,password_hash,role,fiscal_code_hmac,fiscal_code_hmac_key_version,fiscal_code_enc,fiscal_code_enc_key_version) VALUES(@u,@email,@hash,'student',@fh,@fhk,@fe,@fek)", ct,
                    ("@u", user), ("@email", email), ("@hash", passwordHash), ("@fh", fiscalCode.Hmac), ("@fhk", fiscalCode.HmacKeyId), ("@fe", fiscalCode.Cipher), ("@fek", fiscalCode.CipherKeyId));
            }
            catch (MySqlException e) when (e.Number == 1062 && !e.Message.Contains("uq_user_fiscal_code_hmac", StringComparison.Ordinal))
            {
                plan.MarkTaken(email);
                continue;
            }
            catch (MySqlException e) when (e.Number == 1062)
            {
                throw new MembershipConflictException();
            }
            await Execute("INSERT INTO identity_account_state(user_id,generation) VALUES(@u,0)", ct, ("@u", user));
            await Execute("INSERT INTO identity_account_event(id,user_id,action) VALUES(@e,@u,@a)", ct, ("@e", Guid.NewGuid()), ("@u", user), ("@a", IdentityAccountEvents.Provisioned));
            await Execute("INSERT INTO identity_account_event(id,user_id,action) VALUES(@e,@u,@a)", ct, ("@e", Guid.NewGuid()), ("@u", user), ("@a", IdentityAccountEvents.TemporaryPassword));
            return email;
        }
        throw new AccountEmailUnavailableException();
    }

    private async Task RequireOwned(
        string table,
        Guid id,
        CreateInstitutionAccount r,
        TenantContext actor,
        CancellationToken ct
    )
    {
        // Foreign and absent profiles are indistinguishable.
        await using var command = await Command($"SELECT first_name,last_name FROM {table} WHERE institution_id=@i AND id=@p FOR UPDATE", ct, ("@i", actor.InstitutionId), ("@p", id));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new KeyNotFoundException();
        // E2: the linked profile must describe the same person.
        if (!PersonNameComparison.SameName(reader.GetString(0), r.FirstName) || !PersonNameComparison.SameName(reader.GetString(1), r.LastName))
            throw new AccountProfileMismatchException();
    }
}
