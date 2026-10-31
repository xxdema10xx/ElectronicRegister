// Database First additive sidecar; legacy owner shadow mappings remain untouched.
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    private static void ConfigureFormativePlans(ModelBuilder b)
    {
        {
            var e = b.Entity<FormativePlan>();
            e.ToTable("formative_plan");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.StudyPathId).HasColumnName("study_path_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(64)").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(200)").IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint").IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique().HasDatabaseName("uq_fp_code");
            e.HasIndex(x => new { x.InstitutionId, x.StudyPathId, x.Id }).IsUnique().HasDatabaseName("uq_fp_path");
        }

        {
            var e = b.Entity<FormativePlanVersion>();
            e.ToTable("formative_plan_version");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.StudyPathId).HasColumnName("study_path_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.PlanId).HasColumnName("plan_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.VersionNumber).HasColumnName("version_number").HasColumnType("int").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(200)").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasColumnType("enum('Draft','Published')").IsRequired();
            e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint").IsRequired();
            e.Property(x => x.TheoryPlannedMinutes).HasColumnName("theory_planned_minutes").HasColumnType("int").IsRequired();
            e.Property(x => x.InternshipPlannedMinutes).HasColumnName("internship_planned_minutes").HasColumnType("int").IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc").HasColumnType("datetime(6)");
            e.HasIndex(x => new { x.InstitutionId, x.PlanId, x.VersionNumber }).IsUnique().HasDatabaseName("uq_fpv_number");
            e.HasIndex(x => new { x.InstitutionId, x.StudyPathId, x.Id }).IsUnique().HasDatabaseName("uq_fpv_path");
        }

        {
            var e = b.Entity<FormativeUnit>();
            e.ToTable("formative_unit");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.VersionId).HasColumnName("version_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(64)");
            e.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(200)").IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order").HasColumnType("int").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.VersionId, x.Id }).IsUnique().HasDatabaseName("uq_fu_version");
            e.HasIndex(x => new { x.InstitutionId, x.VersionId, x.DisplayOrder }).IsUnique().HasDatabaseName("uq_fu_order");
            e.HasIndex(x => new { x.InstitutionId, x.VersionId, x.Code }).IsUnique().HasDatabaseName("uq_fu_code");
        }

        {
            var e = b.Entity<FormativeModule>();
            e.ToTable("formative_module");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.VersionId).HasColumnName("version_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.UnitId).HasColumnName("unit_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(64)");
            e.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(200)").IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order").HasColumnType("int").IsRequired();
            e.Property(x => x.PlannedMinutes).HasColumnName("planned_minutes").HasColumnType("int").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.VersionId, x.Id }).IsUnique().HasDatabaseName("uq_fm_version");
            e.HasIndex(x => new { x.InstitutionId, x.UnitId, x.DisplayOrder }).IsUnique().HasDatabaseName("uq_fm_order");
            e.HasIndex(x => new { x.InstitutionId, x.UnitId, x.Code }).IsUnique().HasDatabaseName("uq_fm_code");
        }

        {
            var e = b.Entity<FormativeModuleCompetency>();
            e.ToTable("formative_module_competency");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.VersionId).HasColumnName("version_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ModuleId).HasColumnName("module_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("varchar(4000)").IsRequired();
            e.Property(x => x.DisplayOrder).HasColumnName("display_order").HasColumnType("int").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.ModuleId, x.DisplayOrder }).IsUnique().HasDatabaseName("uq_fmc_order");
        }

        {
            var e = b.Entity<FormativeModuleContent>();
            e.ToTable("formative_module_content");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.VersionId).HasColumnName("version_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ModuleId).HasColumnName("module_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("varchar(4000)").IsRequired();
            e.Property(x => x.DisplayOrder).HasColumnName("display_order").HasColumnType("int").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.ModuleId, x.DisplayOrder }).IsUnique().HasDatabaseName("uq_fmt_order");
        }

        {
            var e = b.Entity<CourseEditionFormativePlan>();
            e.ToTable("course_edition_formative_plan");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.StudyPathId).HasColumnName("study_path_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.CourseEditionId).HasColumnName("course_edition_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.VersionId).HasColumnName("version_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.CourseEditionId }).IsUnique().HasDatabaseName("uq_cefp_edition");
        }

        {
            var e = b.Entity<FormativePlanEvent>();
            e.ToTable("formative_plan_event");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.PlanId).HasColumnName("plan_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.VersionId).HasColumnName("version_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.CourseEditionId).HasColumnName("course_edition_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Operation).HasColumnName("operation").HasColumnType("varchar(40)").IsRequired();
            e.Property(x => x.RevisionBefore).HasColumnName("revision_before").HasColumnType("bigint");
            e.Property(x => x.RevisionAfter).HasColumnName("revision_after").HasColumnType("bigint").IsRequired();
            e.Property(x => x.RequestHash).HasColumnName("request_hash").HasColumnType("char(64)").IsRequired();
            e.Property(x => x.BeforeJson).HasColumnName("before_json").HasColumnType("json");
            e.Property(x => x.AfterJson).HasColumnName("after_json").HasColumnType("json").IsRequired();
            e.Property(x => x.Reason).HasColumnName("reason").HasColumnType("varchar(500)").IsRequired();
            e.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.PlanId, x.OccurredAtUtc, x.Id }).HasDatabaseName("idx_fpe_history");
        }
    }
}
