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

// One transaction: user, identity state and events, active membership (security state
// by trigger), roles, profile and its link, audited Member onboarding event. The
// temporary password is only ever returned to the creating request.
internal sealed class InstitutionAccountRepository(
    ElectronicRegisterContext db,
    IInstitutionWriteAuthorization authorization,
    IInstitutionStudentRepository students,
    IInstitutionTeacherRepository teachers,
    IOnboardingFaultObserver faults
) : IInstitutionAccountRepository
{
    private const string Action = "AccountCreated";

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

    private static string Intent(TenantContext actor, CreateInstitutionAccount r) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        actor.InstitutionId, actor.UserId, actor.MembershipId, r.EventId, r.Email,
        Roles = r.Roles.Select(x => x.ToString()).Order(StringComparer.Ordinal).ToArray(),
        r.StudentId, r.TeacherId, r.FirstName, r.LastName, r.ClassId, r.Reason
    }))));

    private sealed record Stored(Guid UserId, Guid MembershipId, string[] Roles, Guid? StudentId, Guid? TeacherId);

    public async Task<InstitutionAccountCreated> CreateAsync(TenantContext actor, CreateInstitutionAccount r, string passwordHash, string temporaryPassword, CancellationToken ct = default)
    {
        var hash = Intent(actor, r);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Exclusive tenant mutex first (the profile-link trigger also locks the institution),
        // then the actor's current membership, roles, state and identity generation.
        await authorization.LockExamAsync(actor, ct);
        InstitutionAccountRules.RequireAllowed(actor, r.Roles);
        await using (var prior = await Command("SELECT institution_id,action,intent_hash,result_json FROM institution_onboarding_event WHERE id=@e", ct, ("@e", r.EventId)))
        await using (var reader = await prior.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetGuid(0) != actor.InstitutionId || reader.GetString(1) != Action || reader.GetString(2) != hash)
                    throw new MembershipConflictException();
                var stored = JsonSerializer.Deserialize<Stored>(reader.GetString(3))!;
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                // Replays never reveal the password again: a lost response is recovered by a reset.
                return new(r.EventId, stored.UserId, stored.MembershipId, stored.Roles, stored.StudentId, stored.TeacherId, true, null);
            }
        }

        Guid user = Guid.NewGuid(), membership = Guid.NewGuid();
        try
        {
            // Historical required column, never an authorization decision.
            await Execute("INSERT INTO `user`(id,email,password_hash,role) VALUES(@u,@email,@hash,'student')", ct, ("@u", user), ("@email", r.Email), ("@hash", passwordHash));
        }
        catch (MySqlException e) when (e.Number == 1062)
        {
            // Same answer whether the address is an account here, in another institution or
            // reserved in the global email namespace; nothing has been written yet.
            throw new AccountEmailUnavailableException();
        }
        await Execute("INSERT INTO identity_account_state(user_id,generation) VALUES(@u,0)", ct, ("@u", user));
        await Execute("INSERT INTO identity_account_event(id,user_id,action) VALUES(@e,@u,@a)", ct, ("@e", Guid.NewGuid()), ("@u", user), ("@a", IdentityAccountEvents.Provisioned));
        await Execute("INSERT INTO identity_account_event(id,user_id,action) VALUES(@e,@u,@a)", ct, ("@e", Guid.NewGuid()), ("@u", user), ("@a", IdentityAccountEvents.TemporaryPassword));
        await Execute("INSERT INTO institution_membership(id,institution_id,user_id,status,joined_at) VALUES(@m,@i,@u,'active',UTC_TIMESTAMP(6))", ct, ("@m", membership), ("@i", actor.InstitutionId), ("@u", user));
        foreach (var role in r.Roles.Select(x => x.ToString()).Order(StringComparer.Ordinal))
            await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,@r)", ct, ("@m", membership), ("@r", role));

        Guid? student = null, teacher = null;
        if (r.Roles.Contains(InstitutionRole.Student))
        {
            student = r.StudentId ?? Guid.NewGuid();
            if (r.StudentId.HasValue) await RequireOwned("student", student.Value, actor, ct);
            else await students.AddAsync(actor, new Student { Id = student.Value, FirstName = r.FirstName!.Trim(), LastName = r.LastName!.Trim(), ClassId = r.ClassId }, null, ct);
            await Execute("INSERT INTO institution_student_profile(institution_id,membership_id,student_id) VALUES(@i,@m,@s)", ct, ("@i", actor.InstitutionId), ("@m", membership), ("@s", student));
        }
        if (r.Roles.Contains(InstitutionRole.Teacher))
        {
            teacher = r.TeacherId ?? Guid.NewGuid();
            if (r.TeacherId.HasValue) await RequireOwned("teacher", teacher.Value, actor, ct);
            else await teachers.AddAsync(actor, new Teacher { Id = teacher.Value, FirstName = r.FirstName!.Trim(), LastName = r.LastName!.Trim() }, null, ct);
            await Execute("INSERT INTO institution_teacher_profile(institution_id,membership_id,teacher_id) VALUES(@i,@m,@t)", ct, ("@i", actor.InstitutionId), ("@m", membership), ("@t", teacher));
        }

        var roles = r.Roles.Select(x => x.ToString()).Order(StringComparer.Ordinal).ToArray();
        var result = new Stored(user, membership, roles, student, teacher);
        await Execute("INSERT INTO institution_onboarding_event(institution_id,id,actor_kind,actor_user_id,actor_membership_id,action,intent_hash,reason,result_json,occurred_at_utc) VALUES(@i,@e,'Member',@au,@am,@a,@h,@r,@j,UTC_TIMESTAMP(6))", ct,
            ("@i", actor.InstitutionId), ("@e", r.EventId), ("@au", actor.UserId), ("@am", actor.MembershipId), ("@a", Action), ("@h", hash), ("@r", r.Reason), ("@j", JsonSerializer.Serialize(result)));
        await faults.BeforeCommitAsync("AccountCreate", ct);
        await tx.CommitAsync(ct);
        return new(r.EventId, user, membership, roles, student, teacher, false, temporaryPassword);
    }

    private async Task RequireOwned(string table, Guid id, TenantContext actor, CancellationToken ct)
    {
        // Foreign and absent profiles are indistinguishable.
        await using var command = await Command($"SELECT id FROM {table} WHERE institution_id=@i AND id=@p FOR UPDATE", ct, ("@i", actor.InstitutionId), ("@p", id));
        if (await command.ExecuteScalarAsync(ct) is null)
            throw new KeyNotFoundException();
    }
}
