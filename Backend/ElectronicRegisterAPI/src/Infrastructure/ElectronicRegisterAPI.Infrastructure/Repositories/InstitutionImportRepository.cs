using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Persistence;
using ElectronicRegisterAPI.Domain.Persistence.Import;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace ElectronicRegisterAPI.Infrastructure.Repositories;

internal sealed class InstitutionImportRepository(
    ElectronicRegisterContext db,
    IMembershipAuthorizationRepository memberships,
    IInstitutionWriteAuthorization writes,
    IInstitutionImportFeature feature
) : IInstitutionImportRepository
{
    private async Task Access(TenantContext actor, CancellationToken ct)
    {
        if (!feature.IsEnabled)
            throw new UnauthorizedAccessException();
        InstitutionPermissions.Require(actor, InstitutionPermission.UserCreate);
        var m = await memberships.GetAsync(actor.UserId, actor.InstitutionId, actor.MembershipId, ct);
        if (!MembershipAuthorizationRules.Matches(actor, m))
            throw new UnauthorizedAccessException();
    }

    public async Task<T> ExecuteAsync<T>(TenantContext actor, Func<IInstitutionImportSession, Task<T>> action, CancellationToken ct)
    {
        await Access(actor, ct);
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Il batch richiede una propria unità di lavoro.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM institution WHERE id={actor.InstitutionId} FOR UPDATE", ct);
            await writes.LockAsync(actor, ct);
            await Access(actor, ct);
            var result = await action(new Session(db, writes, actor, feature, ct));
            if (!feature.IsEnabled)
                throw new UnauthorizedAccessException();
            await tx.CommitAsync(ct);
            return result;
        }
        catch (MySqlException e)when (e.Number is 1062 or 1451 or 1452 or 1644 or 1213 or 1205)
        {
            throw new ImportConflictException("DatabaseConflict");
        }
        catch (DbUpdateException e)when (e.InnerException is MySqlException { Number: 1062 or 1451 or 1452 or 1644 or 1213 or 1205 })
        {
            throw new ImportConflictException("DatabaseConflict");
        }
    }

    private sealed class Session(
        ElectronicRegisterContext db,
        IInstitutionWriteAuthorization writes,
        TenantContext actor,
        IInstitutionImportFeature feature,
        CancellationToken ct
    ) : IInstitutionImportSession
    {
        private static string Json(object value) => JsonSerializer.Serialize(value);
        private async Task<DbCommand> Command(string sql, params (string Key, object? Value)[] args)
        {
            writes.RequireLocked(actor);
            if (db.Database.GetDbConnection().State != ConnectionState.Open)
                await db.Database.OpenConnectionAsync(ct);
            var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            foreach (var(key, value)in args)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = key;
                p.Value = value is Guid id ? id.ToString("D") : value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }

            return cmd;
        }

        private async Task<List<Dictionary<string, object>>> Rows(string sql, params (string, object? )[] args)
        {
            await using var cmd = await Command(sql, args);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var result = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object>();
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.GetValue(i);
                result.Add(row);
            }

            return result;
        }

        private async Task<int> Execute(string sql, params (string, object? )[] args)
        {
            await using var cmd = await Command(sql, args);
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        private (string, object? )[] Scope => [("@t", actor.InstitutionId), ("@u", actor.UserId), ("@m", actor.MembershipId)];

        private const string Own = "institution_id=@t AND actor_user_id=@u AND actor_membership_id=@m";
        private static string S(Dictionary<string, object> r, string key) => (string)r[key];
        private static long N(Dictionary<string, object> r, string key) => Convert.ToInt64(r[key]);
        private static Guid G(Dictionary<string, object> r, string key) => Guid.Parse(Convert.ToString(r[key])!);
        private static DateTime D(Dictionary<string, object> r, string key) => DateTime.SpecifyKind((DateTime)r[key], DateTimeKind.Utc);
        private static ImportBatch Metadata(Dictionary<string, object> r)
        {
            var state = S(r, "state");
            var expires = D(r, "expires_at_utc");
            if (state == "Preview" && expires <= DateTime.UtcNow)
                state = "Expired";
            return new(G(r, "id"), Enum.Parse<ImportCategory>(S(r, "category")), state, N(r, "revision"), S(r, "file_sha256"), S(r, "preview_digest"), (int)N(r, "total_rows"), (int)N(r, "valid_rows"), (int)N(r, "invalid_rows"), D(r, "created_at_utc"), expires, r["policy_revision"] is DBNull ? null : N(r, "policy_revision"), JsonSerializer.Deserialize<ImportIssue[]>(S(r, "warnings_json"))!);
        }

        public async Task<ImportStoredBatch> GetAsync(Guid id)
        {
            var r = (await Rows("SELECT * FROM institution_import_batch WHERE id=@id AND " + Own, Scope.Append(("@id", (object? )id)).ToArray())).SingleOrDefault() ?? throw new KeyNotFoundException();
            var b = Metadata(r);
            return new(b, b.State == "Preview" && r["payload"] is not DBNull ? JsonSerializer.Deserialize<ImportPayload>(S(r, "payload")) : null, r["result_json"] is DBNull ? null : JsonSerializer.Deserialize<ImportResult>(S(r, "result_json")));
        }

        public async Task<ImportBatchPage> ListAsync(int page, int size)
        {
            if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
                throw new ArgumentException();
            var total = N((await Rows("SELECT COUNT(*) n FROM institution_import_batch WHERE " + Own, Scope)).Single(), "n");
            var rows = await Rows("SELECT id,category,state,revision,file_sha256,preview_digest,total_rows,valid_rows,invalid_rows,created_at_utc,expires_at_utc,policy_revision,warnings_json FROM institution_import_batch WHERE " + Own + " ORDER BY created_at_utc DESC,id LIMIT @size OFFSET @offset", Scope.Concat(new[] { ("@size", (object? )size), ("@offset", (object? )((page - 1) * size)) }).ToArray());
            return new(page, size, total, rows.Select(Metadata).ToArray());
        }

        public async Task<IReadOnlyList<ImportClassMatch>> ClassesAsync(string reference, Guid? choice)
        {
            if (choice.HasValue && Guid.TryParse(reference, out var requested) && requested != choice.Value)
                return[];
            Guid? id = choice ?? (Guid.TryParse(reference, out var parsed) ? parsed : null);
            var rows = await Rows("SELECT c.id,c.name,c.biennium_study_path_id,c.course_edition_id,COALESCE((SELECT MAX(e.revision) FROM institution_class_event e WHERE e.institution_id=c.institution_id AND e.class_id=c.id),0) revision FROM class c WHERE c.institution_id=@t AND (@id IS NULL AND c.name=@name OR @id IS NOT NULL AND c.id=@id AND (@byid=1 OR c.name=@name)) ORDER BY c.id LIMIT 3", ("@t", actor.InstitutionId), ("@id", id), ("@name", reference), ("@byid", Guid.TryParse(reference, out _)));
            return rows.Select(r => new ImportClassMatch(G(r, "id"), Json(new { Id = G(r, "id"), Name = S(r, "name"), Path = r["biennium_study_path_id"], Edition = r["course_edition_id"], Revision = N(r, "revision") }))).ToArray();
        }

        public async Task<bool> ExternalExistsAsync(ImportCategory category, string reference) => (await Rows("SELECT 1 FROM institution_import_external_reference WHERE institution_id=@t AND category=@c AND reference=@r LIMIT 1", ("@t", actor.InstitutionId), ("@c", category.ToString()), ("@r", reference))).Count > 0;
        public async Task<bool> HomonymExistsAsync(ImportCategory category, string first, string last)
        {
            var table = category == ImportCategory.Student ? "student" : "teacher";
            return (await Rows($"SELECT 1 FROM {table} WHERE institution_id=@t AND first_name=@f AND last_name=@l LIMIT 1", ("@t", actor.InstitutionId), ("@f", first), ("@l", last))).Count > 0;
        }

        public async Task<bool> PreviouslyImportedAsync(string hash, ImportCategory category, Guid except) => (await Rows("SELECT 1 FROM institution_import_batch WHERE institution_id=@t AND category=@c AND file_sha256=@h AND state='Completed' AND id<>@id LIMIT 1", ("@t", actor.InstitutionId), ("@c", category.ToString()), ("@h", hash), ("@id", except))).Count > 0;
        public async Task<ImportReplay?> ReplayAsync(Guid eventId)
        {
            var r = (await Rows("SELECT request_hash,result_json FROM institution_import_event WHERE institution_id=@t AND id=@id", ("@t", actor.InstitutionId), ("@id", eventId))).SingleOrDefault();
            return r is null ? null : new(S(r, "request_hash"), S(r, "result_json"));
        }

        private string Payload(ImportPayload payload)
        {
            var json = Json(payload);
            if (Encoding.UTF8.GetByteCount(json) > feature.Settings.MaxStageBytes)
                throw new ImportInputException("StageTooLarge", "Dati di staging oltre il limite.");
            return json;
        }

        public async Task CreateAsync(
            ImportBatch b,
            ImportPayload payload,
            Guid eventId,
            string requestHash
        )
        {
            if (N((await Rows("SELECT COUNT(*) n FROM institution_import_batch WHERE " + Own + " AND payload IS NOT NULL", Scope)).Single(), "n") >= feature.Settings.MaxPendingPreviews)
                throw new ImportConflictException("PreviewQuotaExceeded");
            await Execute("INSERT INTO institution_import_batch(id,institution_id,actor_user_id,actor_membership_id,category,state,revision,file_sha256,preview_digest,total_rows,valid_rows,invalid_rows,payload,warnings_json,policy_revision,created_at_utc,expires_at_utc) VALUES(@id,@t,@u,@m,@c,'Preview',1,@file,@digest,@total,@valid,@invalid,@payload,@warnings,@policy,@created,@expires)", Scope.Concat(new (string, object? )[] { ("@id", b.Id), ("@c", b.Category.ToString()), ("@file", b.FileSha256), ("@digest", b.PreviewDigest), ("@total", b.TotalRows), ("@valid", b.ValidRows), ("@invalid", b.InvalidRows), ("@payload", Payload(payload)), ("@warnings", Json(b.Warnings)), ("@policy", b.PolicyRevision), ("@created", b.CreatedAtUtc), ("@expires", b.ExpiresAtUtc) }).ToArray());
            await Event(b.Id, 1, eventId, "Previewed", requestHash, b with { Columns = null, Mapping = null }, "Preview caricata e validata");
        }

        public async Task RemapAsync(
            ImportBatch b,
            ImportPayload payload,
            Guid eventId,
            string requestHash
        )
        {
            if (await Execute("UPDATE institution_import_batch SET revision=revision+1,preview_digest=@digest,valid_rows=@valid,invalid_rows=@invalid,payload=@payload,warnings_json=@warnings,policy_revision=@policy WHERE id=@id AND " + Own + " AND revision=@expected AND state='Preview' AND expires_at_utc>UTC_TIMESTAMP(6)", Scope.Concat(new (string, object? )[] { ("@id", b.Id), ("@digest", b.PreviewDigest), ("@valid", b.ValidRows), ("@invalid", b.InvalidRows), ("@payload", Payload(payload)), ("@warnings", Json(b.Warnings)), ("@policy", b.PolicyRevision), ("@expected", b.Revision - 1) }).ToArray()) != 1)
                throw new ImportConflictException("StalePreview");
            await Event(b.Id, b.Revision, eventId, "Remapped", requestHash, b with { Columns = null, Mapping = null }, "Mapping aggiornato");
        }

        public async Task AddCreatedAsync(
            Guid batch,
            ImportCategory category,
            ImportCreatedRow row,
            string? externalReference
        )
        {
            if (externalReference is not null)
                await Execute("INSERT INTO institution_import_external_reference(institution_id,category,reference,student_id,teacher_id) VALUES(@t,@c,@r,@s,@d)", ("@t", actor.InstitutionId), ("@c", category.ToString()), ("@r", externalReference), ("@s", row.StudentId), ("@d", row.TeacherId));
            await Execute("INSERT INTO institution_import_item(institution_id,batch_id,row_number,category,student_id,teacher_id,email_assignment_id,external_reference) VALUES(@t,@b,@n,@c,@s,@d,@a,@r)", ("@t", actor.InstitutionId), ("@b", batch), ("@n", row.RowNumber), ("@c", category.ToString()), ("@s", row.StudentId), ("@d", row.TeacherId), ("@a", row.EmailAssignmentId), ("@r", externalReference));
        }

        public async Task CompleteAsync(
            Guid batch,
            long revision,
            Guid eventId,
            string requestHash,
            ImportResult result,
            string reason
        )
        {
            if (await Execute("UPDATE institution_import_batch SET state='Completed',revision=revision+1,payload=NULL,result_json=@result WHERE id=@id AND " + Own + " AND revision=@r AND state='Preview' AND expires_at_utc>UTC_TIMESTAMP(6)", Scope.Concat(new (string, object? )[] { ("@id", batch), ("@r", revision), ("@result", Json(result)) }).ToArray()) != 1)
                throw new ImportConflictException("StalePreview");
            await Event(batch, result.Revision, eventId, "Confirmed", requestHash, result, reason);
        }

        private async Task Event(
            Guid batch,
            long revision,
            Guid id,
            string action,
            string hash,
            object result,
            string reason
        )
        {
            await Execute("INSERT INTO institution_import_event(institution_id,id,batch_id,revision,action,actor_kind,actor_user_id,actor_membership_id,request_hash,result_json,reason,occurred_at_utc) VALUES(@t,@id,@b,@r,@a,'User',@u,@m,@hash,@result,@reason,UTC_TIMESTAMP(6))", Scope.Concat(new (string, object? )[] { ("@id", id), ("@b", batch), ("@r", revision), ("@a", action), ("@hash", hash), ("@result", Json(result)), ("@reason", reason) }).ToArray());
        }
    }
}
