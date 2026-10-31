// Database First sidecar. Student/Teacher composite ownership FKs remain SQL-only as in MT3A.
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    private static void ConfigureInstitutionImports(ModelBuilder b)
    {
        {
            var e = b.Entity<InstitutionImportBatch>();
            e.ToTable("institution_import_batch");
            e.HasKey(x => new { x.Id });
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Category).HasColumnName("category").HasColumnType("varchar(7)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
            e.Property(x => x.State).HasColumnName("state").HasColumnType("enum('Preview','Completed','Expired')").IsRequired();
            e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint").IsRequired();
            e.Property(x => x.FileSha256).HasColumnName("file_sha256").HasColumnType("char(64)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
            e.Property(x => x.PreviewDigest).HasColumnName("preview_digest").HasColumnType("char(64)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
            e.Property(x => x.TotalRows).HasColumnName("total_rows").HasColumnType("int").IsRequired();
            e.Property(x => x.ValidRows).HasColumnName("valid_rows").HasColumnType("int").IsRequired();
            e.Property(x => x.InvalidRows).HasColumnName("invalid_rows").HasColumnType("int").IsRequired();
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("json");
            e.Property(x => x.ResultJson).HasColumnName("result_json").HasColumnType("json");
            e.Property(x => x.WarningsJson).HasColumnName("warnings_json").HasColumnType("json").IsRequired();
            e.Property(x => x.PolicyRevision).HasColumnName("policy_revision").HasColumnType("bigint");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.Id }).IsUnique().HasDatabaseName("uq_import_batch_owner");
            e.HasIndex(x => new { x.InstitutionId, x.Id, x.Category }).IsUnique().HasDatabaseName("uq_import_batch_category");
            e.HasIndex(x => new { x.InstitutionId, x.ActorMembershipId, x.ActorUserId, x.CreatedAtUtc, x.Id }).HasDatabaseName("idx_import_batch_actor");
            e.HasIndex(x => new { x.InstitutionId, x.Category, x.FileSha256, x.State }).HasDatabaseName("idx_import_batch_digest");
            e.HasIndex(x => new { x.State, x.ExpiresAtUtc, x.Id }).HasDatabaseName("idx_import_batch_expiry");
            e.HasIndex(x => new { x.InstitutionId, x.Category, x.PolicyRevision }).HasDatabaseName("fk_import_batch_policy");
            e.HasOne<InstitutionEmailPolicy>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.Category, x.PolicyRevision }).HasPrincipalKey(x => new { x.InstitutionId, x.Category, x.Revision }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_batch_policy");
            e.HasOne<Institution>().WithMany().HasForeignKey(x => new { x.InstitutionId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_batch_institution");
            e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.ActorMembershipId, x.ActorUserId }).HasPrincipalKey(x => new { x.InstitutionId, x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_batch_actor");
        }

        {
            var e = b.Entity<InstitutionImportExternalReference>();
            e.ToTable("institution_import_external_reference");
            e.HasKey(x => new { x.InstitutionId, x.Category, x.Reference });
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Category).HasColumnName("category").HasColumnType("varchar(7)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
            e.Property(x => x.Reference).HasColumnName("reference").HasColumnType("varchar(100)").HasCharSet("utf8mb4").UseCollation("utf8mb4_bin").IsRequired();
            e.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.TeacherId).HasColumnName("teacher_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.HasIndex(x => new { x.InstitutionId, x.StudentId }).HasDatabaseName("fk_import_external_student");
            e.HasIndex(x => new { x.InstitutionId, x.TeacherId }).HasDatabaseName("fk_import_external_teacher");
            e.HasOne<Institution>().WithMany().HasForeignKey(x => new { x.InstitutionId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_external_institution");
        }

        {
            var e = b.Entity<InstitutionImportItem>();
            e.ToTable("institution_import_item");
            e.HasKey(x => new { x.InstitutionId, x.BatchId, x.RowNumber });
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.RowNumber).HasColumnName("row_number").HasColumnType("int").IsRequired();
            e.Property(x => x.Category).HasColumnName("category").HasColumnType("varchar(7)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
            e.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.TeacherId).HasColumnName("teacher_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.EmailAssignmentId).HasColumnName("email_assignment_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.ExternalReference).HasColumnName("external_reference").HasColumnType("varchar(100)").HasCharSet("utf8mb4").UseCollation("utf8mb4_bin");
            e.HasIndex(x => new { x.InstitutionId, x.BatchId, x.Category }).HasDatabaseName("fk_import_item_batch");
            e.HasIndex(x => new { x.InstitutionId, x.StudentId }).HasDatabaseName("fk_import_item_student");
            e.HasIndex(x => new { x.InstitutionId, x.TeacherId }).HasDatabaseName("fk_import_item_teacher");
            e.HasIndex(x => new { x.InstitutionId, x.EmailAssignmentId }).HasDatabaseName("fk_import_item_assignment");
            e.HasIndex(x => new { x.InstitutionId, x.Category, x.ExternalReference }).HasDatabaseName("fk_import_item_external");
            e.HasOne<InstitutionImportBatch>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.BatchId, x.Category }).HasPrincipalKey(x => new { x.InstitutionId, x.Id, x.Category }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_item_batch");
            e.HasOne<InstitutionEmailAssignment>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.EmailAssignmentId }).HasPrincipalKey(x => new { x.InstitutionId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_item_assignment");
            e.HasOne<InstitutionImportExternalReference>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.Category, x.ExternalReference }).HasPrincipalKey(x => new { x.InstitutionId, x.Category, x.Reference }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_item_external");
        }

        {
            var e = b.Entity<InstitutionImportEvent>();
            e.ToTable("institution_import_event");
            e.HasKey(x => new { x.InstitutionId, x.Id });
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint").IsRequired();
            e.Property(x => x.Action).HasColumnName("action").HasColumnType("varchar(12)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
            e.Property(x => x.ActorKind).HasColumnName("actor_kind").HasColumnType("enum('User','Retention')").IsRequired();
            e.Property(x => x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.RequestHash).HasColumnName("request_hash").HasColumnType("char(64)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
            e.Property(x => x.ResultJson).HasColumnName("result_json").HasColumnType("json").IsRequired();
            e.Property(x => x.Reason).HasColumnName("reason").HasColumnType("varchar(500)").HasCharSet("utf8mb4").UseCollation("utf8mb4_unicode_ci").IsRequired();
            e.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.BatchId, x.Revision }).IsUnique().HasDatabaseName("uq_import_event_revision");
            e.HasIndex(x => new { x.InstitutionId, x.ActorMembershipId, x.ActorUserId }).HasDatabaseName("fk_import_event_actor");
            e.HasOne<InstitutionImportBatch>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.BatchId }).HasPrincipalKey(x => new { x.InstitutionId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_event_batch");
            e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.ActorMembershipId, x.ActorUserId }).HasPrincipalKey(x => new { x.InstitutionId, x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_import_event_actor");
        }
    }
}
