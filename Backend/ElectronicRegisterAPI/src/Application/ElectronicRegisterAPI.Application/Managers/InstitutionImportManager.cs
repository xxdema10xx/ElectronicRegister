using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionImportManager(InstitutionSchoolAuthorization access,IInstitutionImportRepository repository,
    IInstitutionImportFileReader reader,IInstitutionImportRules rules,IInstitutionEmailRepository emails,
    IInstitutionStudentRepository students,IInstitutionTeacherRepository teachers,InstitutionImportFeature feature) : IInstitutionImportManager
{
    private async Task<TenantContext> Actor(CancellationToken ct)
    {if(!feature.IsEnabled)throw new UnauthorizedAccessException();return await access.PermissionAsync(InstitutionPermission.UserCreate,ct);}
    private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string Intent(TenantContext actor,object request)=>Hash(new{actor.InstitutionId,actor.UserId,actor.MembershipId,Request=request});
    private static void EventId(Guid id){if(id==Guid.Empty)throw new ArgumentException("EventId obbligatorio.");}
    private static void Live(ImportStoredBatch stored)
    {if(stored.Batch.State!="Preview"||stored.Batch.ExpiresAtUtc<=DateTime.UtcNow||stored.Payload is null)throw new ImportConflictException(stored.Batch.State=="Completed"?"AlreadyCompleted":"PreviewExpired");}
    private static string Digest(ImportPayload payload)=>Hash(new{payload.Table.Sha256,payload.Settings,payload.Mapping,payload.Rows,payload.PolicyId,payload.PolicyRevision,payload.Warnings});
    private static ImportBatch Metadata(Guid id,ImportPayload payload,long revision,DateTime created,DateTime expires)
    {
        var invalid=payload.Rows.Count(r=>r.Data.Status is ImportRowStatus.Invalid or ImportRowStatus.AmbiguousReference);
        if(payload.Mapping.Issues.Any(i=>!i.Warning))invalid=payload.Rows.Count;
        return new(id,payload.Settings.Category,"Preview",revision,payload.Table.Sha256,Digest(payload),payload.Rows.Count,payload.Rows.Count-invalid,invalid,created,expires,payload.PolicyRevision,payload.Warnings,payload.Table.Headers,payload.Mapping.Columns);
    }
    private async Task<ImportPayload> Prepare(TenantContext actor,IInstitutionImportSession session,ImportTable table,ImportPreviewSettings settings,Guid except,CancellationToken ct)
    {
        if(!Enum.IsDefined(settings.Category)||!Enum.IsDefined(settings.EmailMode)||table.Rows.Count==0)throw new ImportInputException("NoDataRows","Il file deve contenere righe dati e una categoria valida.");
        if(settings.Classes is not null&&(settings.Classes.Count>table.Rows.Count||settings.Classes.Any(c=>c is null||c.ClassId==Guid.Empty||!table.Rows.Any(r=>r.RowNumber==c.RowNumber))||settings.Classes.Select(c=>c.RowNumber).Distinct().Count()!=settings.Classes.Count))throw new ImportInputException("InvalidClassChoice","Scelta classe non valida.");
        if(settings.EmailCandidates is not null&&(settings.EmailMode!=ImportEmailMode.ReserveInstitutionalAddress||settings.EmailCandidates.Count>table.Rows.Count||settings.EmailCandidates.Any(c=>c is null||c.CandidateOffset is <0 or >99||!table.Rows.Any(r=>r.RowNumber==c.RowNumber))||settings.EmailCandidates.Select(c=>c.RowNumber).Distinct().Count()!=settings.EmailCandidates.Count))throw new ImportInputException("InvalidEmailCandidate","Scelta candidato email non valida.");
        var mapping=rules.Map(table.Headers,settings.Category,settings.Mapping);var warnings=mapping.Issues.ToList();
        EmailPolicyVersion? policy=null;
        if(settings.EmailMode==ImportEmailMode.ReserveInstitutionalAddress)
        {
            try{policy=await emails.PolicyAsync(actor,settings.Category==ImportCategory.Student?EmailCategory.Student:EmailCategory.Teacher,null,ct);}
            catch(KeyNotFoundException){ /* Validation returns a stable per-row missing-policy error. */ }
        }
        var preliminary=mapping.Issues.Any(i=>!i.Warning)?table.Rows.Select(r=>new ImportValidatedRow(r.RowNumber,ImportRowStatus.Invalid,null,null,null,null,null,mapping.Issues)).ToArray():rules.Validate(table,settings.Category,mapping,settings.EmailMode,policy,settings.EmailCandidates?.ToDictionary(c=>c.RowNumber,c=>c.CandidateOffset));
        var rows=new List<ImportPreparedRow>();var classes=new Dictionary<string,IReadOnlyList<ImportClassMatch>>(StringComparer.Ordinal);
        foreach(var row in preliminary)
        {
            ct.ThrowIfCancellationRequested();var issues=row.Issues.Where(i=>i.Code!="RequiresTenantResolution").ToList();Guid? classId=null;string? fingerprint=null;IReadOnlyList<Guid>? candidates=null;
            var choice=settings.Classes?.SingleOrDefault(c=>c.RowNumber==row.RowNumber)?.ClassId;
            if(row.ClassReference is {} reference)
            {
                var key=reference+"\0"+choice;
                if(!classes.TryGetValue(key,out var matches)){matches=await session.ClassesAsync(reference,choice);classes.Add(key,matches);}
                if(matches.Count==0)issues.Add(new("Class","ClassNotFound","Classe non disponibile nella Fondazione selezionata."));
                else if(matches.Count>1){candidates=matches.Select(c=>c.Id).ToArray();issues.Add(new("Class","AmbiguousClass","Specificare un identificativo classe senza ambiguità."));}
                else{classId=matches[0].Id;fingerprint=matches[0].Fingerprint;}
            }
            else if(choice.HasValue)issues.Add(new("Class","InvalidClassChoice","Il file non contiene un riferimento classe per la riga."));
            if(row.ExternalReference is {} external&&await session.ExternalExistsAsync(settings.Category,external))issues.Add(new("ExternalReference","ExternalReferenceExists","Riferimento esterno già registrato nella Fondazione."));
            if(row.FirstName is {} first&&row.LastName is {} last&&await session.HomonymExistsAsync(settings.Category,first,last))issues.Add(new("Row","PossibleExistingPerson","Possibile omonimia con un'anagrafica esistente; nessun merge o aggiornamento.",true));
            var status=issues.Any(i=>!i.Warning)?issues.Any(i=>i.Code=="AmbiguousClass")?ImportRowStatus.AmbiguousReference:ImportRowStatus.Invalid:issues.Any()?ImportRowStatus.Warning:ImportRowStatus.Valid;
            rows.Add(new(row with{Status=status,Issues=issues},classId,fingerprint,candidates));
        }
        if(await session.PreviouslyImportedAsync(table.Sha256,settings.Category,except))warnings.Add(new("File","FilePreviouslyImported","File identico già importato nel tenant: una ripetizione richiede intenzione esplicita e motivazione.",true));
        return new(table,settings,mapping,rows,policy?.PolicyId,policy?.Revision,warnings);
    }
    public async Task<ImportBatch> PreviewAsync(Stream file,string fileName,ImportParsingOptions parsing,ImportPreviewSettings settings,CancellationToken ct)
    {
        var actor=await Actor(ct);var table=await reader.ReadAsync(file,fileName,parsing,feature.Limits,ct);
        return await repository.ExecuteAsync(actor,async session=>
        {
            var id=Guid.NewGuid();var payload=await Prepare(actor,session,table,settings,id,ct);var now=DateTime.UtcNow;var b=Metadata(id,payload,1,now,now.AddMinutes(feature.TtlMinutes));
            await session.CreateAsync(b,payload,Guid.NewGuid(),Intent(actor,new{Op="Preview",Id=id,table.Sha256,Settings=settings}));return b;
        },ct);
    }
    public async Task<ImportBatch> GetAsync(Guid id,CancellationToken ct)
    {var actor=await Actor(ct);return await repository.ExecuteAsync(actor,async s=>{var b=await s.GetAsync(id);return b.Batch with{Columns=b.Payload?.Table.Headers,Mapping=b.Payload?.Mapping.Columns};},ct);}
    public async Task<ImportRowsPage> RowsAsync(Guid id,int page,int size,CancellationToken ct)
    {
        if(page<1||size is <1 or >100||(long)(page-1)*size>int.MaxValue)throw new ArgumentException();
        var actor=await Actor(ct);return await repository.ExecuteAsync(actor,async s=>{var b=await s.GetAsync(id);Live(b);return new ImportRowsPage(page,size,b.Payload!.Rows.Count,b.Payload.Rows.Skip((page-1)*size).Take(size).ToArray());},ct);
    }
    public async Task<ImportBatchPage> ListAsync(int page,int size,CancellationToken ct)=>await repository.ExecuteAsync(await Actor(ct),s=>s.ListAsync(page,size),ct);
    public async Task<ImportBatch> RemapAsync(Guid id,ImportRemap request,CancellationToken ct)
    {
        EventId(request.EventId);var actor=await Actor(ct);var hash=Intent(actor,new{Op="Remap",Id=id,Request=request});
        return await repository.ExecuteAsync(actor,async session=>
        {
            var stored=await session.GetAsync(id);var replay=await session.ReplayAsync(request.EventId);
            if(replay is not null){if(replay.RequestHash!=hash)throw new ImportConflictException("EventIdConflict");return JsonSerializer.Deserialize<ImportBatch>(replay.ResultJson)!;}
            Live(stored);if(stored.Batch.Revision!=request.ExpectedRevision||request.Settings.Category!=stored.Batch.Category||request.ExpectedRevision==long.MaxValue)throw new ImportConflictException("StalePreview");
            var payload=await Prepare(actor,session,stored.Payload!.Table,request.Settings,id,ct);var next=Metadata(id,payload,stored.Batch.Revision+1,stored.Batch.CreatedAtUtc,stored.Batch.ExpiresAtUtc);
            await session.RemapAsync(next,payload,request.EventId,hash);return next;
        },ct);
    }
    public async Task<ImportResult> ConfirmAsync(Guid id,ImportConfirmation request,CancellationToken ct)
    {
        EventId(request.EventId);if(request.Reason is {Length:>500}||request.Reason?.Any(char.IsControl)==true||request.Reason is not null&&string.IsNullOrWhiteSpace(request.Reason)||request.AllowRepeatedFile&&string.IsNullOrWhiteSpace(request.Reason))throw new ArgumentException("Motivazione obbligatoria per la ripetizione.");
        var actor=await Actor(ct);var hash=Intent(actor,new{Op="Confirm",Id=id,Request=request});
        return await repository.ExecuteAsync(actor,async session=>
        {
            var stored=await session.GetAsync(id);var replay=await session.ReplayAsync(request.EventId);
            if(replay is not null){if(replay.RequestHash!=hash)throw new ImportConflictException("EventIdConflict");return JsonSerializer.Deserialize<ImportResult>(replay.ResultJson)!;}
            Live(stored);if(stored.Batch.Revision!=request.ExpectedRevision||stored.Batch.PreviewDigest!=request.PreviewDigest||stored.Batch.Revision==long.MaxValue)throw new ImportConflictException("StalePreview");
            if(stored.Batch.InvalidRows>0)throw new ImportInputException("RowsInvalid","Correggere tutte le righe prima di confermare.");
            var payload=await Prepare(actor,session,stored.Payload!.Table,stored.Payload.Settings,id,ct);
            if(Digest(payload)!=stored.Batch.PreviewDigest)throw new ImportConflictException("PreviewContextChanged");
            if((payload.Warnings.Any(i=>i.Warning)||payload.Rows.Any(r=>r.Data.Issues.Any(i=>i.Warning)))&&!request.AcknowledgeWarnings)throw new ImportConflictException("WarningsNotAcknowledged");
            if(await session.PreviouslyImportedAsync(payload.Table.Sha256,payload.Settings.Category,id)&&!request.AllowRepeatedFile)throw new ImportConflictException("RepeatedFileNotAuthorized");
            var created=new List<ImportCreatedRow>();
            foreach(var row in payload.Rows.OrderBy(r=>r.Data.RowNumber))
            {
                ct.ThrowIfCancellationRequested();Guid? student=null,teacher=null,assignment=null;
                if(payload.Settings.EmailMode==ImportEmailMode.ReserveInstitutionalAddress)
                {
                    var a=await emails.CreatePersonAsync(actor,new(Guid.NewGuid(),payload.Settings.Category==ImportCategory.Student?EmailCategory.Student:EmailCategory.Teacher,payload.PolicyRevision!.Value,row.Data.FirstName!,row.Data.LastName!,row.ClassId),ct);
                    if(a.Email!=row.Data.EmailPreview)throw new ImportConflictException("EmailAvailabilityChanged");
                    student=a.StudentId;teacher=a.TeacherId;assignment=a.Id;
                }
                else if(payload.Settings.Category==ImportCategory.Student){student=Guid.NewGuid();await students.AddAsync(actor,new Student{Id=student.Value,FirstName=row.Data.FirstName!,LastName=row.Data.LastName!,ClassId=row.ClassId},null,ct);}
                else{teacher=Guid.NewGuid();await teachers.AddAsync(actor,new Teacher{Id=teacher.Value,FirstName=row.Data.FirstName!,LastName=row.Data.LastName!},null,ct);}
                var item=new ImportCreatedRow(row.Data.RowNumber,student,teacher,assignment);await session.AddCreatedAsync(id,payload.Settings.Category,item,row.Data.ExternalReference);created.Add(item);
            }
            var result=new ImportResult(id,stored.Batch.Revision+1,created.Count,created);await session.CompleteAsync(id,stored.Batch.Revision,request.EventId,hash,result,request.Reason??"Importazione CreateOnly AllOrNothing");return result;
        },ct);
    }
    public async Task<ImportResult> ResultAsync(Guid id,CancellationToken ct)
    {var actor=await Actor(ct);return await repository.ExecuteAsync(actor,async s=>(await s.GetAsync(id)).Result??throw new ImportConflictException("NotCompleted"),ct);}
    public async Task<byte[]> TemplateAsync(ImportCategory category,CancellationToken ct)
    {
        _=await Actor(ct);if(!Enum.IsDefined(category))throw new ArgumentException();
        var csv=category==ImportCategory.Student?"Nome;Cognome;Classe;RiferimentoEsterno\r\nMario;Rossi;;STU-001\r\nGiulia;Bianchi;;STU-002\r\n":"Nome;Cognome;RiferimentoEsterno\r\nMario;Rossi;DOC-001\r\nAnna Maria;Verdi;DOC-002\r\n";
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }
}
