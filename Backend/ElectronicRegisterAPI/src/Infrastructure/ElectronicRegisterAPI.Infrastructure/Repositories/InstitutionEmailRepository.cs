using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionEmailRepository(ElectronicRegisterContext db, IMembershipAuthorizationRepository memberships,
    IInstitutionWriteAuthorization writes, InstitutionEmailFeature feature, IInstitutionEmailGenerator generator,
    IInstitutionStudentRepository students, IInstitutionTeacherRepository teachers) : IInstitutionEmailRepository
{
    private static bool Admin(TenantContext t) => t.Roles.Contains(InstitutionRole.InstitutionAdmin);
    private static bool Staff(TenantContext t) => t.Roles.Contains(InstitutionRole.Staff);
    private static void Administrator(TenantContext t) { if (!Admin(t)) throw new UnauthorizedAccessException(); }
    private static void Operators(TenantContext t) { if (!Admin(t) && !Staff(t)) throw new UnauthorizedAccessException(); }
    private static void Category(TenantContext t, EmailCategory c)
    { Operators(t); if (!Enum.IsDefined(c)) throw new ArgumentException(); if (!Admin(t) && c is not (EmailCategory.Student or EmailCategory.Teacher)) throw new UnauthorizedAccessException(); }
    private static void Reason(string value) { if (string.IsNullOrWhiteSpace(value) || value.Length > 500 || value.Any(char.IsControl)) throw new ArgumentException("Motivazione non valida."); }
    private static void Page(int page, int size) { if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue) throw new ArgumentException(); }
    private async Task Access(TenantContext t, CancellationToken ct)
    {
        if (!feature.Enabled) throw new UnauthorizedAccessException("Modulo email non abilitato.");
        var m = await memberships.GetAsync(t.UserId, t.InstitutionId, t.MembershipId, ct);
        if (m is null || m.Status != MembershipStatus.Active || m.InstitutionStatus != InstitutionStatus.Active ||
            m.SecurityGeneration is null or < 0 || m.SecurityGeneration != t.SecurityGeneration || !t.Roles.SetEquals(m.Roles)) throw new UnauthorizedAccessException();
    }
    private async Task<DbCommand> Command(string sql, CancellationToken ct, params (string Key, object? Value)[] args)
    {
        if (db.Database.GetDbConnection().State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);
        var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.CommandText = sql;
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var (key, value) in args) { var p = cmd.CreateParameter(); p.ParameterName = key; p.Value = value is Guid guid ? guid.ToString("D") : value ?? DBNull.Value; cmd.Parameters.Add(p); }
        return cmd;
    }
    private async Task<List<Dictionary<string, object>>> Rows(string sql, CancellationToken ct, params (string, object?)[] args)
    {
        await using var cmd = await Command(sql, ct, args); await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Dictionary<string, object>>();
        while (await reader.ReadAsync(ct)) { var row = new Dictionary<string, object>(); for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.GetValue(i); rows.Add(row); }
        return rows;
    }
    private async Task<int> Execute(string sql, CancellationToken ct, params (string, object?)[] args)
    { await using var cmd = await Command(sql, ct, args); return await cmd.ExecuteNonQueryAsync(ct); }
    private static string Text(Dictionary<string, object> r, string key) => (string)r[key];
    private static Guid GuidValue(Dictionary<string, object> r, string key) => Guid.Parse(Convert.ToString(r[key])!);
    private static Guid? Optional(Dictionary<string, object> r, string key) => r[key] is DBNull ? null : GuidValue(r, key);
    private static long Number(Dictionary<string, object> r, string key) => Convert.ToInt64(r[key]);
    private static DateTime Utc(Dictionary<string, object> r, string key) => DateTime.SpecifyKind((DateTime)r[key], DateTimeKind.Utc);
    private static EmailPolicyVersion Policy(Dictionary<string, object> r) => new(GuidValue(r,"policy_id"), Enum.Parse<EmailCategory>(Text(r,"category")), Number(r,"revision"), Text(r,"state"), JsonSerializer.Deserialize<EmailTemplate>(Text(r,"configuration"))!, GuidValue(r,"actor_user_id"),GuidValue(r,"actor_membership_id"),Utc(r,"created_at_utc"));
    private static EmailAssignment Assignment(Dictionary<string, object> r) => new(GuidValue(r,"id"),Optional(r,"student_id"),Optional(r,"teacher_id"),Optional(r,"membership_id"),Enum.Parse<EmailCategory>(Text(r,"category")),Number(r,"policy_revision"),Text(r,"email"),Text(r,"state"),Number(r,"revision"),Text(r,"first_name_snapshot"),Text(r,"last_name_snapshot"),Utc(r,"created_at_utc"),Utc(r,"updated_at_utc"));
    private static string Visible => "a.institution_id=@tenant AND (@admin=1 OR @staff=1 AND (a.student_id IS NOT NULL OR a.teacher_id IS NOT NULL) OR a.membership_id=@member OR EXISTS(SELECT 1 FROM institution_student_profile p WHERE p.institution_id=@tenant AND p.membership_id=@member AND p.student_id=a.student_id) OR EXISTS(SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=@tenant AND p.membership_id=@member AND p.teacher_id=a.teacher_id))";
    private static (string,object?)[] Scope(TenantContext t) => [("@tenant",t.InstitutionId),("@member",t.MembershipId),("@admin",Admin(t)),("@staff",Staff(t))];
    private async Task<T> Write<T>(TenantContext actor, Guid eventId, object intent, Func<string,Task<T>> action, CancellationToken ct)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("EventId obbligatorio.");
        if (db.Database.CurrentTransaction is not null) writes.RequireLocked(actor);
        await Access(actor, ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { actor.UserId, actor.MembershipId, actor.InstitutionId, Intent = intent }))));
        await using var owned = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // IAM-first mutex also serializes policy versions and recipient identity reconciliation.
            await Execute("SELECT id FROM institution WHERE id=@tenant FOR UPDATE", ct, ("@tenant",actor.InstitutionId));
            await writes.LockAsync(actor, ct);
            await Access(actor, ct);
            var prior = (await Rows("SELECT request_hash,after_json FROM institution_email_event WHERE institution_id=@tenant AND id=@id", ct, ("@tenant",actor.InstitutionId),("@id",eventId))).SingleOrDefault();
            T result;
            if (prior is not null)
            {
                if (Text(prior,"request_hash") != hash) throw new BusinessRuleException("EventId già utilizzato.");
                result = JsonSerializer.Deserialize<T>(Text(prior,"after_json"))!;
            }
            else result = await action(hash);
            if (owned is not null) await owned.CommitAsync(ct);
            return result;
        }
        catch (MySqlException e) when (e.Number is 1062 or 1451 or 1452 or 1644 or 1213 or 1205)
        { throw new BusinessRuleException("Conflitto nella configurazione o assegnazione email."); }
    }
    private async Task Event(TenantContext t, Guid id, string operation, EmailCategory category, long policyRevision,
        Guid? assignment, string hash, object? before, object after, string reason, CancellationToken ct)
    {
        await Execute("INSERT INTO institution_email_event(institution_id,id,actor_user_id,actor_membership_id,operation,category,policy_revision,assignment_id,request_hash,before_json,after_json,reason,occurred_at_utc) VALUES(@t,@id,@u,@m,@op,@cat,@rev,@a,@hash,@before,@after,@reason,UTC_TIMESTAMP(6))",ct,
            ("@t",t.InstitutionId),("@id",id),("@u",t.UserId),("@m",t.MembershipId),("@op",operation),("@cat",category.ToString()),("@rev",policyRevision),("@a",assignment),("@hash",hash),("@before",before is null ? null : JsonSerializer.Serialize(before)),("@after",JsonSerializer.Serialize(after)),("@reason",reason));
    }
    public async Task<IReadOnlyList<EmailPolicyVersion>> PoliciesAsync(TenantContext t, CancellationToken ct)
    {
        await Access(t,ct); Operators(t);
        return (await Rows("SELECT p.* FROM institution_email_policy p WHERE p.institution_id=@t AND (@admin=1 OR p.category IN ('Student','Teacher')) AND NOT EXISTS(SELECT 1 FROM institution_email_policy n WHERE n.institution_id=p.institution_id AND n.category=p.category AND n.revision>p.revision) ORDER BY p.category",ct,("@t",t.InstitutionId),("@admin",Admin(t)))).Select(Policy).ToArray();
    }
    public async Task<EmailPolicyVersion> PolicyAsync(TenantContext t, EmailCategory category, long? revision, CancellationToken ct)
    {
        await Access(t,ct); Category(t,category);
        return await LoadPolicy(t,category,revision,ct);
    }
    private async Task<EmailPolicyVersion> LoadPolicy(TenantContext t, EmailCategory category, long? revision, CancellationToken ct) =>
        Policy((await Rows("SELECT * FROM institution_email_policy WHERE institution_id=@t AND category=@c AND (@r IS NULL OR revision=@r) ORDER BY revision DESC LIMIT 1",ct,("@t",t.InstitutionId),("@c",category.ToString()),("@r",revision))).SingleOrDefault() ?? throw new KeyNotFoundException());
    public async Task<EmailPolicyVersion> WritePolicyAsync(TenantContext t, EmailCategory category, EmailPolicyWrite r, CancellationToken ct)
    {
        Administrator(t); if (!Enum.IsDefined(category) || r.ExpectedRevision < 0 || r.ExpectedRevision == long.MaxValue || r.State is not ("Draft" or "Published" or "Disabled")) throw new ArgumentException();
        generator.Validate(r.Template); Reason(r.Reason);
        return await Write(t,r.EventId,new { Op="PolicyWritten",Category=category,Request=r },async hash =>
        {
            var prior = (await Rows("SELECT * FROM institution_email_policy WHERE institution_id=@t AND category=@c ORDER BY revision DESC LIMIT 1",ct,("@t",t.InstitutionId),("@c",category.ToString()))).SingleOrDefault();
            if ((prior is null ? 0 : Number(prior,"revision")) != r.ExpectedRevision) throw new BusinessRuleException("Revision della policy obsoleta.");
            var policyId = prior is null ? Guid.NewGuid() : GuidValue(prior,"policy_id"); var next = r.ExpectedRevision+1;
            await Execute("INSERT INTO institution_email_policy(institution_id,policy_id,category,revision,state,configuration,actor_user_id,actor_membership_id,created_at_utc) VALUES(@t,@id,@c,@r,@s,@json,@u,@m,UTC_TIMESTAMP(6))",ct,("@t",t.InstitutionId),("@id",policyId),("@c",category.ToString()),("@r",next),("@s",r.State),("@json",JsonSerializer.Serialize(r.Template)),("@u",t.UserId),("@m",t.MembershipId));
            var result=await LoadPolicy(t,category,next,ct);
            await Event(t,r.EventId,"PolicyWritten",category,next,null,hash,prior is null ? null : Policy(prior),result,r.Reason,ct); return result;
        },ct);
    }
    public async Task<EmailAssignment> ReserveAsync(TenantContext t, EmailReserve r, CancellationToken ct)
    {
        Category(t,r.Category);
        if(r.Recipient is null)throw new ArgumentException();
        var ids = new[]{r.Recipient.StudentId,r.Recipient.TeacherId,r.Recipient.MembershipId};
        if(ids.Count(v=>v.HasValue)!=1 || ids.Any(v=>v==Guid.Empty) || r.PolicyRevision<1)throw new ArgumentException();
        if(!Admin(t) && r.Recipient.MembershipId.HasValue)throw new UnauthorizedAccessException();
        return await Write(t,r.EventId,new{Op="Assigned",Request=r},hash=>ReserveCore(t,r,hash,"Assigned",ct),ct);
    }
    public async Task<EmailAssignment> CreatePersonAsync(TenantContext t,EmailCreatePerson r,CancellationToken ct)
    {
        Category(t,r.Category);
        if(r.Category is not(EmailCategory.Student or EmailCategory.Teacher) || r.PolicyRevision<1 ||
            r.ClassId==Guid.Empty || r.Category==EmailCategory.Teacher && r.ClassId.HasValue ||
            string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName) ||
            r.FirstName.Length>100 || r.LastName.Length>100 || r.FirstName.Any(char.IsControl) || r.LastName.Any(char.IsControl))throw new ArgumentException();
        return await Write(t,r.EventId,new{Op="PersonCreatedAndAssigned",Request=r},async hash=>
        {
            var person=Guid.NewGuid();
            // Existing anagraphic repositories participate in this already IAM-locked transaction.
            // Event replay is checked before creation, so retries return the same person and address.
            if(r.Category==EmailCategory.Student)
                await students.AddAsync(t,new Student{Id=person,FirstName=r.FirstName,LastName=r.LastName,ClassId=r.ClassId},null,ct);
            else await teachers.AddAsync(t,new Teacher{Id=person,FirstName=r.FirstName,LastName=r.LastName},null,ct);
            var recipient=r.Category==EmailCategory.Student?new EmailRecipient(person,null,null):new EmailRecipient(null,person,null);
            return await ReserveCore(t,new(r.EventId,r.Category,r.PolicyRevision,recipient),hash,"PersonCreatedAndAssigned",ct);
        },ct);
    }
    private async Task<EmailAssignment> ReserveCore(TenantContext t,EmailReserve r,string hash,string operation,CancellationToken ct)
    {
            await Execute("SELECT id FROM institution_email_mutex WHERE id=1 FOR UPDATE",ct);
            var policy=await LoadPolicy(t,r.Category,null,ct);
            if(policy.Revision!=r.PolicyRevision || policy.State!="Published")throw new BusinessRuleException("Policy non pubblicata o revision obsoleta.");
            Guid? student=r.Recipient.StudentId, teacher=r.Recipient.TeacherId, member=r.Recipient.MembershipId;
            string first,last;
            if(student.HasValue || teacher.HasValue)
            {
                if(r.Recipient.FirstName is not null || r.Recipient.LastName is not null || r.Category!=(student.HasValue?EmailCategory.Student:EmailCategory.Teacher))throw new ArgumentException();
                var table=student.HasValue?"student":"teacher"; var profile=student.HasValue?"institution_student_profile":"institution_teacher_profile"; var column=student.HasValue?"student_id":"teacher_id";
                var person=(await Rows($"SELECT s.first_name,s.last_name,p.membership_id FROM {table} s LEFT JOIN {profile} p ON p.institution_id=s.institution_id AND p.{column}=s.id WHERE s.institution_id=@t AND s.id=@id",ct,("@t",t.InstitutionId),("@id",student??teacher))).SingleOrDefault()??throw new KeyNotFoundException();
                first=Text(person,"first_name"); last=Text(person,"last_name"); member=Optional(person,"membership_id");
            }
            else
            {
                if(r.Category is not(EmailCategory.Staff or EmailCategory.InstitutionAdmin))throw new ArgumentException();
                if((await Rows("SELECT id FROM institution_membership WHERE institution_id=@t AND id=@id AND status='active'",ct,("@t",t.InstitutionId),("@id",member))).Count!=1)throw new KeyNotFoundException();
                first=r.Recipient.FirstName??"";last=r.Recipient.LastName??"";
                if(first.Length>256 || last.Length>256 || first.Any(char.IsControl)||last.Any(char.IsControl))throw new ArgumentException();
            }
            var id=Guid.NewGuid(); var input=new EmailGenerationInput(first,last);
            for(var attempt=0;attempt<(policy.Template.Collision==EmailCollisionStrategy.Reject?1:policy.Template.MaximumAttempts);attempt++)
            {
                var generated=generator.Candidate(policy.Template,input,attempt);
                try
                {
                    await Execute("INSERT INTO institution_email_assignment(id,institution_id,student_id,teacher_id,membership_id,category,policy_revision,email,state,revision,first_name_snapshot,last_name_snapshot,actor_user_id,actor_membership_id,created_at_utc,updated_at_utc) VALUES(@id,@t,@s,@d,@m,@cat,@pr,@email,'reserved',1,@first,@last,@u,@actor,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))",ct,("@id",id),("@t",t.InstitutionId),("@s",student),("@d",teacher),("@m",member),("@cat",r.Category.ToString()),("@pr",r.PolicyRevision),("@email",generated.Email),("@first",first),("@last",last),("@u",t.UserId),("@actor",t.MembershipId));
                }
                catch(MySqlException e)when(e.Number==1062){ continue; /* Unique namespace INSERT is authoritative, including concurrent User writers. */ }
                var result=await LoadAssignment(t,id,ct);
                await Event(t,r.EventId,operation,r.Category,r.PolicyRevision,id,hash,null,result,"Assegnazione secondo policy pubblicata",ct);
                return result;
            }
            throw new BusinessRuleException("Indirizzo non disponibile; revisione amministrativa richiesta.");
    }
    private async Task<EmailAssignment> LoadAssignment(TenantContext t, Guid id, CancellationToken ct) => Assignment((await Rows("SELECT a.* FROM institution_email_assignment a WHERE a.id=@id AND "+Visible,ct,Scope(t).Append(("@id",(object?)id)).ToArray())).SingleOrDefault()??throw new KeyNotFoundException());
    public async Task<EmailAssignment> AssignmentAsync(TenantContext t,Guid id,CancellationToken ct){await Access(t,ct);return await LoadAssignment(t,id,ct);}
    public async Task<EmailPage<EmailAssignment>> AssignmentsAsync(TenantContext t,int page,int size,CancellationToken ct)
    {
        Page(page,size);await Access(t,ct);
        var total=Number((await Rows("SELECT COUNT(*) n FROM institution_email_assignment a WHERE "+Visible,ct,Scope(t))).Single(),"n");
        var rows=await Rows("SELECT a.* FROM institution_email_assignment a WHERE "+Visible+" ORDER BY a.created_at_utc,a.id LIMIT @size OFFSET @offset",ct,Scope(t).Concat(new[]{("@size",(object?)size),("@offset",(object?)((page-1)*size))}).ToArray());
        return new(page,size,total,rows.Select(Assignment).ToArray());
    }
    public async Task<EmailAssignment> OverrideAsync(TenantContext t,Guid id,EmailOverride r,CancellationToken ct)
    {
        Administrator(t);Reason(r.Reason);if(r.ExpectedRevision<1 || r.ExpectedRevision==long.MaxValue)throw new ArgumentException();
        return await Write(t,r.EventId,new{Op="Overridden",Id=id,Request=r},async hash =>
        {
            await Execute("SELECT id FROM institution_email_mutex WHERE id=1 FOR UPDATE",ct);
            var before=await LoadAssignment(t,id,ct);var p=await LoadPolicy(t,before.Category,null,ct);
            if(before.Revision!=r.ExpectedRevision || p.Revision!=r.PolicyRevision || p.State!="Published")throw new BusinessRuleException("Revision obsoleta.");
            var email=generator.ValidateAddress(r.Email,p.Template.Domain);
            if(email==before.Email)throw new BusinessRuleException("Un override richiede un nuovo indirizzo non già riservato.");
            await Execute("UPDATE institution_email_assignment SET email=@email,state='reserved',revision=revision+1,policy_revision=@pr,actor_user_id=@u,actor_membership_id=@m,updated_at_utc=UTC_TIMESTAMP(6) WHERE institution_id=@t AND id=@id AND revision=@r",ct,("@email",email),("@pr",p.Revision),("@u",t.UserId),("@m",t.MembershipId),("@t",t.InstitutionId),("@id",id),("@r",r.ExpectedRevision));
            var result=await LoadAssignment(t,id,ct);await Event(t,r.EventId,"Overridden",before.Category,p.Revision,id,hash,before,result,r.Reason,ct);return result;
        },ct);
    }
    public async Task<EmailAssignment> RetireAsync(TenantContext t,Guid id,EmailRetire r,CancellationToken ct)
    {
        Administrator(t);Reason(r.Reason);if(r.ExpectedRevision<1 || r.ExpectedRevision==long.MaxValue)throw new ArgumentException();
        return await Write(t,r.EventId,new{Op="Retired",Id=id,Request=r},async hash =>
        {
            var before=await LoadAssignment(t,id,ct);if(before.Revision!=r.ExpectedRevision || before.State!="reserved")throw new BusinessRuleException("Revision o stato non valido.");
            await Execute("UPDATE institution_email_assignment SET state='retired',revision=revision+1,actor_user_id=@u,actor_membership_id=@m,updated_at_utc=UTC_TIMESTAMP(6) WHERE institution_id=@t AND id=@id",ct,("@u",t.UserId),("@m",t.MembershipId),("@t",t.InstitutionId),("@id",id));
            var result=await LoadAssignment(t,id,ct);await Event(t,r.EventId,"Retired",before.Category,before.PolicyRevision,id,hash,before,result,r.Reason,ct);return result;
        },ct);
    }
    public async Task<IReadOnlyList<EmailHistory>> HistoryAsync(TenantContext t,Guid? assignment,EmailCategory? category,int page,int size,CancellationToken ct)
    {
        Administrator(t);Page(page,size);await Access(t,ct);if(assignment is null && category is null)throw new ArgumentException();
        if(assignment.HasValue)_=await LoadAssignment(t,assignment.Value,ct);
        return (await Rows("SELECT * FROM institution_email_event WHERE institution_id=@t AND (@a IS NULL OR assignment_id=@a) AND (@c IS NULL OR category=@c) ORDER BY occurred_at_utc,id LIMIT @size OFFSET @offset",ct,("@t",t.InstitutionId),("@a",assignment),("@c",category?.ToString()),("@size",size),("@offset",(page-1)*size))).Select(r=>new EmailHistory(GuidValue(r,"id"),Text(r,"operation"),GuidValue(r,"actor_user_id"),GuidValue(r,"actor_membership_id"),r["before_json"] is DBNull?null:Text(r,"before_json"),Text(r,"after_json"),Text(r,"reason"),Utc(r,"occurred_at_utc"))).ToArray();
    }
}
