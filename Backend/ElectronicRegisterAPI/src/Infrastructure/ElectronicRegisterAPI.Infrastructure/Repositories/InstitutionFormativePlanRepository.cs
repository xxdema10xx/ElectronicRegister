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

internal sealed class InstitutionFormativePlanRepository(ElectronicRegisterContext db,
    IMembershipAuthorizationRepository memberships, IInstitutionWriteAuthorization writes,
    FormativePlanFeature feature, IFormativeDraftBuilder builder, IFormativePlanFaultObserver faults) : IInstitutionFormativePlanRepository
{
    private static bool Admin(TenantContext t) => t.Roles.Contains(InstitutionRole.InstitutionAdmin);
    private static bool Catalog(TenantContext t) => Admin(t) || t.Roles.Contains(InstitutionRole.Staff);
    private static void RequireAdmin(TenantContext t) { if (!Admin(t)) throw new UnauthorizedAccessException(); }
    private static void RequireCatalog(TenantContext t) { if (!Catalog(t)) throw new UnauthorizedAccessException(); }
    private static void Page(int page,int size) { if(page<1 || size is <1 or >100 || (long)(page-1)*size>int.MaxValue) throw new ArgumentException(); }
    private async Task Access(TenantContext t,CancellationToken ct)
    {
        if(!feature.Enabled) throw new UnauthorizedAccessException();
        var m=await memberships.GetAsync(t.UserId,t.InstitutionId,t.MembershipId,ct);
        if(m is null || m.Status!=MembershipStatus.Active || m.InstitutionStatus!=InstitutionStatus.Active ||
            m.SecurityGeneration is null or <0 || m.SecurityGeneration!=t.SecurityGeneration || !t.Roles.SetEquals(m.Roles)) throw new UnauthorizedAccessException();
    }
    private async Task<DbCommand> Command(string sql,CancellationToken ct, params (string,object?)[] args)
    {
        if(db.Database.GetDbConnection().State!=ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);
        var cmd=db.Database.GetDbConnection().CreateCommand(); cmd.CommandText=sql; cmd.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
        foreach(var(key,value) in args) {var p=cmd.CreateParameter();p.ParameterName=key;p.Value=value is Guid g?g.ToString("D"):value??DBNull.Value;cmd.Parameters.Add(p);}
        return cmd;
    }
    private async Task<List<Dictionary<string,object>>> Rows(string sql,CancellationToken ct,params (string,object?)[] args)
    {
        await using var cmd=await Command(sql,ct,args);await using var reader=await cmd.ExecuteReaderAsync(ct);
        var result=new List<Dictionary<string,object>>();while(await reader.ReadAsync(ct)) {var r=new Dictionary<string,object>();for(var i=0;i<reader.FieldCount;i++)r[reader.GetName(i)]=reader.GetValue(i);result.Add(r);}return result;
    }
    private async Task Execute(string sql,CancellationToken ct,params (string,object?)[] args)
    {await using var cmd=await Command(sql,ct,args);await cmd.ExecuteNonQueryAsync(ct);}
    private static Guid G(Dictionary<string,object> r,string key)=>Guid.Parse(Convert.ToString(r[key])!);
    private static Guid? NG(Dictionary<string,object> r,string key)=>r[key] is DBNull?null:G(r,key);
    private static long L(Dictionary<string,object> r,string key)=>Convert.ToInt64(r[key]);
    private static int I(Dictionary<string,object> r,string key)=>Convert.ToInt32(r[key]);
    private static string S(Dictionary<string,object> r,string key)=>Convert.ToString(r[key])!;
    private static string? NS(Dictionary<string,object> r,string key)=>r[key] is DBNull?null:S(r,key);
    private static DateTime U(Dictionary<string,object> r,string key)=>DateTime.SpecifyKind((DateTime)r[key],DateTimeKind.Utc);
    private static BusinessRuleException Conflict()=>new("Conflitto sul piano formativo.");
    private static FormativePlanInfo Info(Dictionary<string,object> r)=>new(G(r,"id"),G(r,"study_path_id"),S(r,"code"),S(r,"name"),NS(r,"description"),L(r,"revision"),U(r,"created_at_utc"));

    private async Task<FormativeReceipt> Write(TenantContext t,Guid eventId,string reason,object intent,Func<string,Task<FormativeReceipt>> action,CancellationToken ct)
    {
        RequireAdmin(t);
        if(eventId==Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length>500 || reason.Contains('\0')) throw new ArgumentException();
        if(db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Operazione autonoma richiesta.");
        await Access(t,ct);
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{t.UserId,t.MembershipId,t.InstitutionId,Intent=intent}))));
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        try
        {
            // Same IAM-first tenant mutex as email/import, retained until audit and commit.
            await Execute("SELECT id FROM institution WHERE id=@t FOR UPDATE",ct,("@t",t.InstitutionId));
            await writes.LockAsync(t,ct);await Access(t,ct);
            var prior=(await Rows("SELECT request_hash,after_json FROM formative_plan_event WHERE institution_id=@t AND id=@id",ct,("@t",t.InstitutionId),("@id",eventId))).SingleOrDefault();
            FormativeReceipt result;
            if(prior is not null)
            {
                if(S(prior,"request_hash")!=hash)throw Conflict();
                using var document=JsonDocument.Parse(S(prior,"after_json"));
                result=JsonSerializer.Deserialize<FormativeReceipt>(document.RootElement.GetProperty("Receipt").GetRawText())!;
            }
            else result=await action(hash);
            await faults.CheckpointAsync("before-commit",ct);
            await Access(t,ct);await tx.CommitAsync(ct);return result;
        }
        catch(MySqlException e) when(e.Number is 1062 or 1451 or 1452 or 1644 or 1213 or 1205) {throw Conflict();}
        catch(InvalidOperationException) {throw Conflict();}
    }
    private async Task Event(TenantContext t,FormativeReceipt result,string operation,long? beforeRevision,object? before,string hash,string reason,CancellationToken ct)
    {
      object state=result.CourseEditionId is {} editionId
          ? new {CourseEditionId=editionId,VersionId=result.VersionId}
          : result.VersionId is {} versionId ? await LoadVersion(t,versionId,ct) : await LoadPlan(t,result.PlanId,ct);
      await Execute(
        "INSERT INTO formative_plan_event(id,institution_id,plan_id,version_id,course_edition_id,actor_user_id,actor_membership_id,operation,revision_before,revision_after,request_hash,before_json,after_json,reason,occurred_at_utc) VALUES(@id,@t,@p,@v,@e,@u,@m,@op,@rb,@ra,@hash,@before,@after,@reason,UTC_TIMESTAMP(6))",ct,
        ("@id",result.EventId),("@t",t.InstitutionId),("@p",result.PlanId),("@v",result.VersionId),("@e",result.CourseEditionId),
        ("@u",t.UserId),("@m",t.MembershipId),("@op",operation),("@rb",beforeRevision),("@ra",result.Revision),("@hash",hash),
        ("@before",before is null?null:JsonSerializer.Serialize(before)),("@after",JsonSerializer.Serialize(new{Receipt=result,State=state})),("@reason",reason));
      await faults.CheckpointAsync("after-audit",ct);
    }

    public async Task<IReadOnlyList<FormativePlanInfo>> ListAsync(TenantContext t,int page,int size,CancellationToken ct)
    {await Access(t,ct);RequireCatalog(t);Page(page,size);return(await Rows("SELECT * FROM formative_plan WHERE institution_id=@t ORDER BY code,id LIMIT @n OFFSET @o",ct,("@t",t.InstitutionId),("@n",size),("@o",(page-1)*size))).Select(Info).ToArray();}
    private async Task<FormativePlanInfo> LoadPlan(TenantContext t,Guid id,CancellationToken ct)=>Info((await Rows("SELECT * FROM formative_plan WHERE institution_id=@t AND id=@id",ct,("@t",t.InstitutionId),("@id",id))).SingleOrDefault()??throw new KeyNotFoundException());
    public async Task<FormativePlanInfo> GetAsync(TenantContext t,Guid id,CancellationToken ct) {await Access(t,ct);RequireCatalog(t);return await LoadPlan(t,id,ct);}
    private async Task<FormativePlanSnapshot> LoadVersion(TenantContext t,Guid id,CancellationToken ct)
    {
        await using var read=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct):null;
        var r=(await Rows("SELECT * FROM formative_plan_version WHERE institution_id=@t AND id=@id",ct,("@t",t.InstitutionId),("@id",id))).SingleOrDefault()??throw new KeyNotFoundException();
        var units=await Rows("SELECT * FROM formative_unit WHERE institution_id=@t AND version_id=@v ORDER BY display_order,id",ct,("@t",t.InstitutionId),("@v",id));
        var modules=await Rows("SELECT * FROM formative_module WHERE institution_id=@t AND version_id=@v ORDER BY display_order,id",ct,("@t",t.InstitutionId),("@v",id));
        var competencies=await Rows("SELECT * FROM formative_module_competency WHERE institution_id=@t AND version_id=@v ORDER BY display_order,id",ct,("@t",t.InstitutionId),("@v",id));
        var contents=await Rows("SELECT * FROM formative_module_content WHERE institution_id=@t AND version_id=@v ORDER BY display_order,id",ct,("@t",t.InstitutionId),("@v",id));
        FormativeTopic[] Topics(List<Dictionary<string,object>> rows,Guid module)=>rows.Where(x=>G(x,"module_id")==module).Select(x=>new FormativeTopic(G(x,"id"),S(x,"description"),I(x,"display_order"))).ToArray();
        var result=new FormativePlanSnapshot(id,t.InstitutionId,G(r,"study_path_id"),G(r,"plan_id"),I(r,"version_number"),S(r,"name"),Enum.Parse<FormativeVersionStatus>(S(r,"status")),L(r,"revision"),I(r,"theory_planned_minutes"),I(r,"internship_planned_minutes"),
            units.Select(u=>new FormativeUnitSnapshot(G(u,"id"),NS(u,"code"),S(u,"name"),NS(u,"description"),I(u,"display_order"),
                modules.Where(m=>G(m,"unit_id")==G(u,"id")).Select(m=>new FormativeModuleSnapshot(G(m,"id"),NS(m,"code"),S(m,"name"),NS(m,"description"),I(m,"planned_minutes"),I(m,"display_order"),NG(m,"subject_id"),Topics(competencies,G(m,"id")),Topics(contents,G(m,"id")))).ToArray())).ToArray());
        if(read is not null)await read.CommitAsync(ct);
        return FormativePlanRules.Freeze(result) with {CreatedAtUtc=U(r,"created_at_utc"),PublishedAtUtc=r["published_at_utc"] is DBNull?null:U(r,"published_at_utc")};
    }
    public async Task<FormativePlanSnapshot> VersionAsync(TenantContext t,Guid id,CancellationToken ct)
    {await Access(t,ct);RequireCatalog(t);return await LoadVersion(t,id,ct);}
    public async Task<IReadOnlyList<FormativePlanSnapshot>> VersionsAsync(TenantContext t,Guid id,int page,int size,CancellationToken ct)
    {await Access(t,ct);RequireCatalog(t);Page(page,size);await LoadPlan(t,id,ct);var result=new List<FormativePlanSnapshot>();foreach(var r in await Rows("SELECT id FROM formative_plan_version WHERE institution_id=@t AND plan_id=@p ORDER BY version_number,id LIMIT @n OFFSET @o",ct,("@t",t.InstitutionId),("@p",id),("@n",size),("@o",(page-1)*size)))result.Add(await LoadVersion(t,G(r,"id"),ct));return result;}
    private async Task<HashSet<Guid>> Subjects(TenantContext t,FormativePlanSnapshot p,CancellationToken ct)
    {
        var needed=p.Units.SelectMany(u=>u.Modules).Where(m=>m.SubjectId.HasValue).Select(m=>m.SubjectId!.Value).ToHashSet();
        var result=new HashSet<Guid>();
        // Scoped point lookups avoid loading the whole tenant catalog; repeated Subject IDs remain distinct modules.
        foreach(var id in needed)if((await Rows("SELECT id FROM subject WHERE institution_id=@t AND id=@id",ct,("@t",t.InstitutionId),("@id",id))).Count==1)result.Add(id);
        return result;
    }
    public async Task<FormativeHourSummary> ValidateAsync(TenantContext t,Guid id,CancellationToken ct)
    {await Access(t,ct);RequireCatalog(t);var p=await LoadVersion(t,id,ct);return FormativePlanRules.Validate(p,await Subjects(t,p,ct));}
    public Task<FormativeReceipt> CreateAsync(TenantContext t,CreateFormativePlan r,CancellationToken ct)=>Write(t,r.EventId,r.Reason,new{Op="plan.create",Request=r},async hash=>
    {
        var code=FormativePlanRules.PlanCode(r.Code);var name=FormativePlanRules.Name(r.Name);var description=FormativePlanRules.Description(r.Description);
        if((await Rows("SELECT id FROM study_path WHERE institution_id=@t AND id=@id",ct,("@t",t.InstitutionId),("@id",r.StudyPathId))).Count!=1)throw new KeyNotFoundException();
        var id=Guid.NewGuid();await Execute("INSERT INTO formative_plan(id,institution_id,study_path_id,code,name,description,revision,created_at_utc) VALUES(@id,@t,@s,@code,@name,@d,0,UTC_TIMESTAMP(6))",ct,("@id",id),("@t",t.InstitutionId),("@s",r.StudyPathId),("@code",code),("@name",name),("@d",description));
        var result=new FormativeReceipt(r.EventId,id,null,0);await Event(t,result,"plan.create",null,null,hash,r.Reason,ct);return result;
    },ct);
    public Task<FormativeReceipt> RenameAsync(TenantContext t,Guid id,RenameFormativePlan r,CancellationToken ct)=>Write(t,r.EventId,r.Reason,new{Op="plan.rename",Id=id,Request=r},async hash=>
    {
        var before=await LoadPlan(t,id,ct);if(r.ExpectedRevision<0 || before.Revision!=r.ExpectedRevision || before.Revision==long.MaxValue)throw Conflict();
        await Execute("UPDATE formative_plan SET name=@n,description=@d,revision=revision+1 WHERE institution_id=@t AND id=@id",ct,("@n",FormativePlanRules.Name(r.Name)),("@d",FormativePlanRules.Description(r.Description)),("@t",t.InstitutionId),("@id",id));
        var result=new FormativeReceipt(r.EventId,id,null,before.Revision+1);await Event(t,result,"plan.rename",before.Revision,before,hash,r.Reason,ct);return result;
    },ct);
    private async Task<int> Next(TenantContext t,Guid planId,CancellationToken ct)
    {
        await Execute("SELECT id FROM formative_plan WHERE institution_id=@t AND id=@id FOR UPDATE",ct,("@t",t.InstitutionId),("@id",planId));
        var r=(await Rows("SELECT COALESCE(MAX(version_number),0) AS n FROM formative_plan_version WHERE institution_id=@t AND plan_id=@p",ct,("@t",t.InstitutionId),("@p",planId))).Single();
        if(L(r,"n")>=int.MaxValue)throw Conflict();return checked(I(r,"n")+1);
    }
    private Task InsertVersion(TenantContext t,FormativePlanSnapshot p,CancellationToken ct)=>Execute("INSERT INTO formative_plan_version(id,institution_id,study_path_id,plan_id,version_number,name,status,revision,theory_planned_minutes,internship_planned_minutes,created_at_utc) VALUES(@id,@t,@s,@p,@num,@n,'Draft',0,@theory,@intern,UTC_TIMESTAMP(6))",ct,("@id",p.Id),("@t",t.InstitutionId),("@s",p.StudyPathId),("@p",p.PlanId),("@num",p.VersionNumber),("@n",p.Name),("@theory",p.TheoryPlannedMinutes),("@intern",p.InternshipPlannedMinutes));
    public Task<FormativeReceipt> CreateVersionAsync(TenantContext t,Guid id,CreateFormativeVersion r,CancellationToken ct)=>Write(t,r.EventId,r.Reason,new{Op="version.create",Id=id,Request=r},async hash=>
    {
        var plan=await LoadPlan(t,id,ct);var number=await Next(t,id,ct);var version=new FormativePlanSnapshot(Guid.NewGuid(),t.InstitutionId,plan.StudyPathId,id,number,FormativePlanRules.Name(r.Name),FormativeVersionStatus.Draft,0,FormativePlanRules.Minutes(r.TheoryPlannedMinutes,false),FormativePlanRules.Minutes(r.InternshipPlannedMinutes,true),[]);
        await InsertVersion(t,version,ct);var result=new FormativeReceipt(r.EventId,id,version.Id,0);await Event(t,result,"version.create",null,null,hash,r.Reason,ct);return result;
    },ct);
    private async Task InsertStructure(TenantContext t,FormativePlanSnapshot p,CancellationToken ct)
    {
        foreach(var u in p.Units)
        {
            await Execute("INSERT INTO formative_unit(id,institution_id,version_id,code,name,description,display_order) VALUES(@id,@t,@v,@code,@name,@d,@o)",ct,("@id",u.Id),("@t",t.InstitutionId),("@v",p.Id),("@code",u.Code),("@name",u.Name),("@d",u.Description),("@o",u.DisplayOrder));
            foreach(var m in u.Modules)
            {
                await Execute("INSERT INTO formative_module(id,institution_id,version_id,unit_id,subject_id,code,name,description,display_order,planned_minutes) VALUES(@id,@t,@v,@u,@s,@code,@name,@d,@o,@minutes)",ct,("@id",m.Id),("@t",t.InstitutionId),("@v",p.Id),("@u",u.Id),("@s",m.SubjectId),("@code",m.Code),("@name",m.Name),("@d",m.Description),("@o",m.DisplayOrder),("@minutes",m.PlannedMinutes));
                foreach(var(table,topics) in new[]{("formative_module_competency",m.Competencies),("formative_module_content",m.Contents)})foreach(var topic in topics)
                    await Execute($"INSERT INTO {table}(id,institution_id,version_id,module_id,description,display_order) VALUES(@id,@t,@v,@m,@d,@o)",ct,("@id",topic.Id),("@t",t.InstitutionId),("@v",p.Id),("@m",m.Id),("@d",topic.Description),("@o",topic.DisplayOrder));
                await faults.CheckpointAsync("after-module",ct);
            }
        }
    }
    public Task<FormativeReceipt> ReplaceAsync(TenantContext t,Guid id,ReplaceFormativeDraft r,CancellationToken ct)=>Write(t,r.EventId,r.Reason,new{Op="draft.replace",Id=id,Request=r},async hash=>
    {
        await Execute("SELECT id FROM formative_plan_version WHERE institution_id=@t AND id=@id FOR UPDATE",ct,("@t",t.InstitutionId),("@id",id));
        var before=await LoadVersion(t,id,ct);var after=builder.Build(before,r);var validation=FormativePlanRules.Validate(after,await Subjects(t,after,ct));
        // Incomplete drafts are permitted, but malformed references, IDs, text and ordering are not.
        if(validation.Issues.Any(x=>x.Code is not("units.count" or "modules.count" or "competencies.count" or "contents.count" or "theory.total-mismatch") && !(x.Code=="subject.required-or-unavailable" && after.Units.SelectMany(u=>u.Modules).Any(m=>m.Id==x.ResourceId && m.SubjectId is null))))throw new ArgumentException("Struttura bozza non valida.");
        await Execute("UPDATE formative_plan_version SET name=@n,theory_planned_minutes=@theory,internship_planned_minutes=@intern,revision=revision+1 WHERE institution_id=@t AND id=@id",ct,("@n",after.Name),("@theory",after.TheoryPlannedMinutes),("@intern",after.InternshipPlannedMinutes),("@t",t.InstitutionId),("@id",id));
        foreach(var table in new[]{"formative_module_competency","formative_module_content","formative_module","formative_unit"})await Execute($"DELETE FROM {table} WHERE institution_id=@t AND version_id=@v",ct,("@t",t.InstitutionId),("@v",id));
        await InsertStructure(t,after,ct);await faults.CheckpointAsync("after-structure",ct);var result=new FormativeReceipt(r.EventId,before.PlanId,id,before.Revision+1);
        await Event(t,result,"draft.replace",before.Revision,before,hash,r.Reason,ct);return result;
    },ct);
    public Task<FormativeReceipt> PublishAsync(TenantContext t,Guid id,FormativeCommand r,CancellationToken ct)=>Write(t,r.EventId,r.Reason,new{Op="version.publish",Id=id,Request=r},async hash=>
    {
        await Execute("SELECT id FROM formative_plan_version WHERE institution_id=@t AND id=@id FOR UPDATE",ct,("@t",t.InstitutionId),("@id",id));
        var before=await LoadVersion(t,id,ct);FormativePlanRules.RequireEditable(before,r.ExpectedRevision);
        var summary=FormativePlanRules.Validate(before,await Subjects(t,before,ct));
        if(!summary.CanPublish)throw new FormativePublicationException(summary);
        await Execute("UPDATE formative_plan_version SET status='Published',revision=revision+1,published_at_utc=UTC_TIMESTAMP(6) WHERE institution_id=@t AND id=@id",ct,("@t",t.InstitutionId),("@id",id));
        var result=new FormativeReceipt(r.EventId,before.PlanId,id,before.Revision+1);await Event(t,result,"version.publish",before.Revision,before,hash,r.Reason,ct);return result;
    },ct);
    public Task<FormativeReceipt> CloneAsync(TenantContext t,Guid id,FormativeCommand r,CancellationToken ct)=>Write(t,r.EventId,r.Reason,new{Op="version.clone",Id=id,Request=r},async hash=>
    {
        var before=await LoadVersion(t,id,ct);if(before.Status!=FormativeVersionStatus.Published || before.Revision!=r.ExpectedRevision)throw Conflict();
        var copy=FormativePlanRules.CloneDraft(before,await Next(t,before.PlanId,ct));await InsertVersion(t,copy,ct);await InsertStructure(t,copy,ct);await faults.CheckpointAsync("after-structure",ct);
        var result=new FormativeReceipt(r.EventId,before.PlanId,copy.Id,0);await Event(t,result,"version.clone",null,new{SourceVersionId=id},hash,r.Reason,ct);return result;
    },ct);
    public Task<FormativeReceipt> BindAsync(TenantContext t,Guid id,BindFormativeVersion r,CancellationToken ct)=>Write(t,r.EventId,r.Reason,new{Op="edition.bind",Id=id,Request=r},async hash=>
    {
        var edition=(await Rows("SELECT study_path_id FROM course_edition WHERE institution_id=@t AND id=@id FOR UPDATE",ct,("@t",t.InstitutionId),("@id",id))).SingleOrDefault()??throw new KeyNotFoundException();
        var version=await LoadVersion(t,r.VersionId,ct);if(version.Status!=FormativeVersionStatus.Published || version.StudyPathId!=G(edition,"study_path_id"))throw Conflict();
        if((await Rows("SELECT id FROM course_edition_formative_plan WHERE institution_id=@t AND course_edition_id=@id",ct,("@t",t.InstitutionId),("@id",id))).Count!=0)throw Conflict();
        await Execute("INSERT INTO course_edition_formative_plan(id,institution_id,study_path_id,course_edition_id,version_id,created_at_utc) VALUES(@id,@t,@s,@e,@v,UTC_TIMESTAMP(6))",ct,("@id",Guid.NewGuid()),("@t",t.InstitutionId),("@s",version.StudyPathId),("@e",id),("@v",version.Id));
        var result=new FormativeReceipt(r.EventId,version.PlanId,version.Id,0,id);await Event(t,result,"edition.bind",null,null,hash,r.Reason,ct);return result;
    },ct);
    public async Task<FormativePlanSnapshot> EditionPlanAsync(TenantContext t,Guid id,CancellationToken ct)
    {
        await Access(t,ct);
        // Personal access is resolved from the current membership's real profiles, never a supplied person ID.
        var visible=Catalog(t) || (t.Roles.Contains(InstitutionRole.Teacher) && (await Rows("SELECT c.id FROM class c JOIN class_subject cs ON cs.institution_id=c.institution_id AND cs.class_id=c.id JOIN institution_teacher_profile p ON p.institution_id=cs.institution_id AND p.teacher_id=cs.teacher_id WHERE c.institution_id=@t AND c.course_edition_id=@e AND p.membership_id=@m LIMIT 1",ct,("@t",t.InstitutionId),("@e",id),("@m",t.MembershipId))).Count>0)
            || (t.Roles.Contains(InstitutionRole.Student) && (await Rows("SELECT e.id FROM student_enrollment e JOIN institution_student_profile p ON p.institution_id=e.institution_id AND p.student_id=e.student_id WHERE e.institution_id=@t AND e.course_edition_id=@e AND p.membership_id=@m LIMIT 1",ct,("@t",t.InstitutionId),("@e",id),("@m",t.MembershipId))).Count>0);
        if(!visible)throw new KeyNotFoundException();
        var binding=(await Rows("SELECT version_id FROM course_edition_formative_plan WHERE institution_id=@t AND course_edition_id=@e",ct,("@t",t.InstitutionId),("@e",id))).SingleOrDefault()??throw new KeyNotFoundException();
        var version=await LoadVersion(t,G(binding,"version_id"),ct);if(version.Status!=FormativeVersionStatus.Published)throw new KeyNotFoundException();return version;
    }
    public async Task<IReadOnlyList<FormativeHistory>> HistoryAsync(TenantContext t,Guid id,int page,int size,CancellationToken ct)
    {
        await Access(t,ct);RequireAdmin(t);Page(page,size);await LoadPlan(t,id,ct);
        return(await Rows("SELECT * FROM formative_plan_event WHERE institution_id=@t AND plan_id=@p ORDER BY occurred_at_utc,id LIMIT @n OFFSET @o",ct,("@t",t.InstitutionId),("@p",id),("@n",size),("@o",(page-1)*size))).Select(r=>new FormativeHistory(G(r,"id"),NG(r,"version_id"),NG(r,"course_edition_id"),S(r,"operation"),G(r,"actor_user_id"),G(r,"actor_membership_id"),r["revision_before"] is DBNull?null:L(r,"revision_before"),L(r,"revision_after"),NS(r,"before_json"),S(r,"after_json"),S(r,"reason"),U(r,"occurred_at_utc"))).ToArray();
    }
}
