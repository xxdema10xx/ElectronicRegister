// Database First scalar sidecar. SQL owns composite foreign keys; legacy EF owners are unchanged.
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    private static void ConfigureInstitutionCalendar(ModelBuilder b)
    {
        {
            var e = b.Entity<InstitutionCalendarEventRecord>();
            e.ToTable("institution_calendar_event");
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ResourceKind).HasColumnName("resource_kind").HasColumnType("varchar(16)").IsRequired();
            e.Property(x => x.ResourceId).HasColumnName("resource_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Operation).HasColumnName("operation").HasColumnType("varchar(48)").IsRequired();
            e.Property(x => x.RevisionBefore).HasColumnName("revision_before").HasColumnType("bigint(20)");
            e.Property(x => x.RevisionAfter).HasColumnName("revision_after").HasColumnType("bigint(20)").IsRequired();
            e.Property(x => x.RequestHash).HasColumnName("request_hash").HasColumnType("char(64)").IsRequired();
            e.Property(x => x.BeforeJson).HasColumnName("before_json").HasColumnType("json");
            e.Property(x => x.StateJson).HasColumnName("state_json").HasColumnType("json").IsRequired();
            e.Property(x => x.ReceiptJson).HasColumnName("receipt_json").HasColumnType("json").IsRequired();
            e.Property(x => x.Reason).HasColumnName("reason").HasColumnType("varchar(500)").IsRequired();
            e.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.HasKey(x => new { x.Id });
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.Id }).IsUnique().HasDatabaseName("uq_cal_event_owner");
            e.HasIndex(x => new { x.InstitutionId, x.ResourceKind, x.ResourceId, x.RevisionAfter }).IsUnique().HasDatabaseName("uq_cal_resource_revision");
            e.HasIndex(x => new { x.InstitutionId, x.ResourceId, x.OccurredAtUtc, x.Id }).HasDatabaseName("idx_cal_history");
            e.HasIndex(x => new { x.InstitutionId, x.ActorMembershipId, x.ActorUserId }).HasDatabaseName("fk_cal_actor");
        }

        {
            var e = b.Entity<InstitutionCalendarSettingsRecord>();
            e.ToTable("institution_calendar_settings");
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasColumnType("varchar(128)").IsRequired();
            e.Property(x => x.ApprovalLeadDays).HasColumnName("approval_lead_days").HasColumnType("int(11)").IsRequired();
            e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint(20)").IsRequired();
            e.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.HasKey(x => new { x.InstitutionId });
            e.Property(x => x.InstitutionId).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.EventId }).HasDatabaseName("fk_cal_settings_event");
        }

        {
            var e = b.Entity<InstitutionCalendarPeriodRecord>();
            e.ToTable("institution_calendar_period");
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ClassId).HasColumnName("class_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.MonthDate).HasColumnName("month_date").HasColumnType("date").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasColumnType("enum('Draft','Published')").IsRequired();
            e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint(20)").IsRequired();
            e.Property(x => x.PublishedByUserId).HasColumnName("published_by_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.PublishedByMembershipId).HasColumnName("published_by_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc").HasColumnType("datetime(6)");
            e.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.HasKey(x => new { x.Id });
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.Id }).IsUnique().HasDatabaseName("uq_cal_period_owner");
            e.HasIndex(x => new { x.InstitutionId, x.ClassId, x.MonthDate }).IsUnique().HasDatabaseName("uq_cal_period_class");
            e.HasIndex(x => new { x.InstitutionId, x.ClassId, x.Id }).IsUnique().HasDatabaseName("uq_cal_period_lesson");
            e.HasIndex(x => new { x.InstitutionId, x.EventId }).HasDatabaseName("fk_cal_period_event");
            e.HasIndex(x => new { x.InstitutionId, x.PublishedByMembershipId, x.PublishedByUserId }).HasDatabaseName("fk_cal_publisher");
        }

        {
            var e = b.Entity<InstitutionCalendarImportRecord>();
            e.ToTable("institution_calendar_import");
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint(20)").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasColumnType("enum('Preview','Confirmed')").IsRequired();
            e.Property(x => x.FileSha256).HasColumnName("file_sha256").HasColumnType("char(64)").IsRequired();
            e.Property(x => x.PreviewDigest).HasColumnName("preview_digest").HasColumnType("char(64)").IsRequired();
            e.Property(x => x.RawJson).HasColumnName("raw_json").HasColumnType("json").IsRequired();
            e.Property(x => x.SettingsJson).HasColumnName("settings_json").HasColumnType("json").IsRequired();
            e.Property(x => x.PreviewJson).HasColumnName("preview_json").HasColumnType("json").IsRequired();
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("datetime(6)").IsRequired();
            e.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.HasKey(x => new { x.Id });
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.Id }).IsUnique().HasDatabaseName("uq_cal_import_owner");
            e.HasIndex(x => new { x.InstitutionId, x.ActorMembershipId, x.Status, x.ExpiresAtUtc }).HasDatabaseName("idx_cal_import_private");
            e.HasIndex(x => new { x.InstitutionId, x.ActorMembershipId, x.ActorUserId }).HasDatabaseName("fk_cal_import_actor");
            e.HasIndex(x => new { x.InstitutionId, x.EventId }).HasDatabaseName("fk_cal_import_event");
        }

        {
            var e = b.Entity<InstitutionLessonRecord>();
            e.ToTable("institution_lesson");
            e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.PeriodId).HasColumnName("period_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ClassId).HasColumnName("class_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.CourseEditionId).HasColumnName("course_edition_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.VersionId).HasColumnName("version_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ModuleId).HasColumnName("module_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ClassSubjectId).HasColumnName("class_subject_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.OriginalTeacherId).HasColumnName("original_teacher_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.TeacherId).HasColumnName("teacher_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.LocalDate).HasColumnName("local_date").HasColumnType("date").IsRequired();
            e.Property(x => x.StartsAt).HasColumnName("starts_at").HasColumnType("time").IsRequired();
            e.Property(x => x.EndsAt).HasColumnName("ends_at").HasColumnType("time").IsRequired();
            e.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasColumnType("varchar(128)").IsRequired();
            e.Property(x => x.StartUtc).HasColumnName("start_utc").HasColumnType("datetime").IsRequired();
            e.Property(x => x.EndUtc).HasColumnName("end_utc").HasColumnType("datetime").IsRequired();
            e.Property(x => x.Publication).HasColumnName("publication").HasColumnType("enum('Draft','Published')").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasColumnType("enum('Scheduled','Cancelled','Completed')").IsRequired();
            e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint(20)").IsRequired();
            e.Property(x => x.DeliveredMinutes).HasColumnName("delivered_minutes").HasColumnType("int(11)");
            e.Property(x => x.ConfirmedByUserId).HasColumnName("confirmed_by_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.ConfirmedByMembershipId).HasColumnName("confirmed_by_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.ConfirmedAtUtc).HasColumnName("confirmed_at_utc").HasColumnType("datetime(6)");
            e.Property(x => x.RecoveryOfId).HasColumnName("recovery_of_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.ImportId).HasColumnName("import_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x => x.SubstitutionReason).HasColumnName("substitution_reason").HasColumnType("varchar(500)");
            e.Property(x => x.Room).HasColumnName("room").HasColumnType("varchar(200)");
            e.Property(x => x.Description).HasColumnName("description").HasColumnType("varchar(2000)");
            e.Property(x => x.ExternalReference).HasColumnName("external_reference").HasColumnType("varchar(100)");
            e.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.HasKey(x => new { x.Id });
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.Id }).IsUnique().HasDatabaseName("uq_cal_lesson_owner");
            e.HasIndex(x => new { x.InstitutionId, x.ClassId, x.StartUtc, x.EndUtc }).HasDatabaseName("idx_cal_class_time");
            e.HasIndex(x => new { x.InstitutionId, x.TeacherId, x.StartUtc, x.EndUtc }).HasDatabaseName("idx_cal_teacher_time");
            e.HasIndex(x => new { x.InstitutionId, x.ClassId, x.ModuleId, x.Publication, x.Status }).HasDatabaseName("idx_cal_module_hours");
            e.HasIndex(x => new { x.InstitutionId, x.ClassId, x.PeriodId }).HasDatabaseName("fk_cal_lesson_period");
            e.HasIndex(x => new { x.InstitutionId, x.CourseEditionId }).HasDatabaseName("fk_cal_lesson_edition");
            e.HasIndex(x => new { x.InstitutionId, x.VersionId, x.ModuleId }).HasDatabaseName("fk_cal_lesson_module");
            e.HasIndex(x => new { x.InstitutionId, x.SubjectId }).HasDatabaseName("fk_cal_lesson_subject");
            e.HasIndex(x => new { x.InstitutionId, x.ClassSubjectId }).HasDatabaseName("fk_cal_lesson_assignment");
            e.HasIndex(x => new { x.InstitutionId, x.OriginalTeacherId }).HasDatabaseName("fk_cal_lesson_original");
            e.HasIndex(x => new { x.InstitutionId, x.RecoveryOfId }).HasDatabaseName("fk_cal_lesson_recovery");
            e.HasIndex(x => new { x.InstitutionId, x.ImportId }).HasDatabaseName("fk_cal_lesson_import");
            e.HasIndex(x => new { x.InstitutionId, x.EventId }).HasDatabaseName("fk_cal_lesson_event");
            e.HasIndex(x => new { x.InstitutionId, x.ConfirmedByMembershipId, x.ConfirmedByUserId }).HasDatabaseName("fk_cal_confirmer");
        }

        {
            var e = b.Entity<InstitutionCalendarOccupancyRecord>();
            e.ToTable("institution_calendar_occupancy");
            e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.LessonId).HasColumnName("lesson_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.ResourceKind).HasColumnName("resource_kind").HasColumnType("enum('Class','Teacher')").IsRequired();
            e.Property(x => x.ResourceId).HasColumnName("resource_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.Property(x => x.StartUtc).HasColumnName("start_utc").HasColumnType("datetime").IsRequired();
            e.Property(x => x.EndUtc).HasColumnName("end_utc").HasColumnType("datetime").IsRequired();
            e.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
            e.HasKey(x => new { x.InstitutionId, x.LessonId, x.ResourceKind });
            e.Property(x => x.InstitutionId).ValueGeneratedNever();
            e.Property(x => x.LessonId).ValueGeneratedNever();
            e.Property(x => x.ResourceKind).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.ResourceKind, x.ResourceId, x.StartUtc, x.EndUtc }).HasDatabaseName("idx_cal_occupancy_resource");
            e.HasIndex(x => new { x.InstitutionId, x.EventId }).HasDatabaseName("fk_cal_occupancy_event");
        }
    }
}
