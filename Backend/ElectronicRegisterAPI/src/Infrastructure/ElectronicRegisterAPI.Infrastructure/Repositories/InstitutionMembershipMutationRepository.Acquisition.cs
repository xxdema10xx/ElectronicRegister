using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs.Membership;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed partial class InstitutionMembershipMutationRepository
{
    private async Task LockAcquisitionIdentitiesAsync(TenantContext actor, Guid membershipId, CancellationToken ct)
    {
        // Current read, not a consistent snapshot before a global lock wait.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT user_id FROM institution_membership WHERE institution_id=@tenant AND id=@member LOCK IN SHARE MODE";
        foreach (var (name,idValue) in new[] {("@tenant",actor.InstitutionId),("@member",membershipId)})
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = idValue;
            command.Parameters.Add(parameter);
        }
        var value = await command.ExecuteScalarAsync(ct);
        Guid? target = value is null ? null : Guid.Parse(Convert.ToString(value)!);
        // Two institutions can acquire profiles for each other's admins. Order
        // both global user locks identically instead of actor-first/target-second.
        foreach (var id in (target.HasValue ? new[] {actor.UserId,target.Value} : new[] {actor.UserId}).Distinct().OrderBy(id=>id.ToString("D"),StringComparer.Ordinal))
        {
            if(id==actor.UserId)await RequireIdentityAsync(actor,ct);
            else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM `user` WHERE id={id} FOR UPDATE",ct);
        }
    }

    public async Task<ProfileAcquisitionResult> AcquireProfileAsync(
        TenantContext actor,
        Guid membershipId,
        AcquireHistoricalProfile request,
        CancellationToken ct = default
    )
    {
        if (membershipId == Guid.Empty || request.EventId == Guid.Empty || request.ProfileId == Guid.Empty || request.ExpectedRevision < 0 || request.Kind is not ("Student" or "Teacher") || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            throw new ArgumentException("Richiesta acquisizione non valida.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { actor.InstitutionId, actor.UserId, actor.MembershipId, Target = membershipId, request }))));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMembershipsAsync(actor, membershipId, ct);
        await LockAcquisitionIdentitiesAsync(actor, membershipId, ct);
        if (!IsCurrentAdmin(actor, await authorization.GetAsync(actor.UserId, actor.InstitutionId, actor.MembershipId, ct)))
            throw new UnauthorizedAccessException();
        var target = await db.InstitutionMemberships.AsNoTracking().Where(m => m.InstitutionId == actor.InstitutionId && m.Id == membershipId).Select(m => new { m.UserId, m.Status, Roles = m.InstitutionMembershipRoles.Select(r => r.Role).ToList() }).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
        if (target.Status != "active" || !target.Roles.Contains(request.Kind))
            throw new UnauthorizedAccessException();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == target.UserId, ct);
        var prior = await db.Database.SqlQuery<string>($"SELECT request_hash AS Value FROM institution_profile_acquisition WHERE institution_id={actor.InstitutionId} AND id={request.EventId}").SingleOrDefaultAsync(ct);
        if (prior is not null)
        {
            if (prior != hash)
                throw new MembershipConflictException();
            var status = await db.Database.SqlQuery<string>($"SELECT status AS Value FROM institution_profile_acquisition WHERE institution_id={actor.InstitutionId} AND id={request.EventId}").SingleAsync(ct);
            await tx.CommitAsync(ct);
            return new(request.EventId, membershipId, request.ProfileId, request.Kind, status);
        }

        var revision = await db.InstitutionMembershipSecurityStates.AsNoTracking().Where(s => s.InstitutionId == actor.InstitutionId && s.MembershipId == membershipId).Select(s => (long? )s.SecurityGeneration).SingleOrDefaultAsync(ct);
        if (revision != request.ExpectedRevision)
            throw new MembershipConflictException();
        bool owned;
        Guid? existing;
        bool used;
        if (request.Kind == "Student")
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM student WHERE institution_id={actor.InstitutionId} AND id={request.ProfileId} FOR UPDATE", ct);
            owned = await db.Students.AnyAsync(s => s.Id == request.ProfileId && EF.Property<Guid>(s, "InstitutionId") == actor.InstitutionId, ct);
            existing = await db.InstitutionStudentProfiles.Where(p => p.InstitutionId == actor.InstitutionId && p.MembershipId == membershipId).Select(p => (Guid? )p.StudentId).SingleOrDefaultAsync(ct);
            used = await db.InstitutionStudentProfiles.AnyAsync(p => p.StudentId == request.ProfileId && p.MembershipId != membershipId, ct);
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM teacher WHERE institution_id={actor.InstitutionId} AND id={request.ProfileId} FOR UPDATE", ct);
            owned = await db.Teachers.AnyAsync(s => s.Id == request.ProfileId && EF.Property<Guid>(s, "InstitutionId") == actor.InstitutionId, ct);
            existing = await db.InstitutionTeacherProfiles.Where(p => p.InstitutionId == actor.InstitutionId && p.MembershipId == membershipId).Select(p => (Guid? )p.TeacherId).SingleOrDefaultAsync(ct);
            used = await db.InstitutionTeacherProfiles.AnyAsync(p => p.TeacherId == request.ProfileId && p.MembershipId != membershipId, ct);
        }

        if (!owned)
            throw new KeyNotFoundException();
        var historical = request.Kind == "Student" ? user.StudentId : user.TeacherId;
        var outcome = historical != request.ProfileId || used || existing.HasValue && existing != request.ProfileId ? "requires-review" : "linked";
        if (outcome == "linked" && !existing.HasValue)
        {
            if (request.Kind == "Student")
                db.InstitutionStudentProfiles.Add(new() { InstitutionId = actor.InstitutionId, MembershipId = membershipId, StudentId = request.ProfileId });
            else
                db.InstitutionTeacherProfiles.Add(new() { InstitutionId = actor.InstitutionId, MembershipId = membershipId, TeacherId = request.ProfileId });
            await db.SaveChangesAsync(ct);
        }

        Guid? student = request.Kind == "Student" ? request.ProfileId : null;
        Guid? teacher = request.Kind == "Teacher" ? request.ProfileId : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO institution_profile_acquisition(institution_id,id,actor_user_id,actor_membership_id,target_user_id,target_membership_id,student_id,teacher_id,status,request_hash,reason) VALUES({actor.InstitutionId},{request.EventId},{actor.UserId},{actor.MembershipId},{target.UserId},{membershipId},{student},{teacher},{outcome},{hash},{request.Reason})", ct);
        await tx.CommitAsync(ct);
        return new(request.EventId, membershipId, request.ProfileId, request.Kind, outcome);
    }
}
