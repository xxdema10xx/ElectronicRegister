// Reproduced by migrations/mt4/Reconcile-Mt4c.py from a fresh database scaffold.
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Infrastructure.Persistence;
public partial class ElectronicRegisterContext
{
    public virtual DbSet<CourseAdministrationEvent> CourseAdministrationEvents { get; set; }
    private static void ConfigureMt4c(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstitutionMembership>().HasAlternateKey(e=>new {e.InstitutionId,e.Id,e.UserId}).HasName("uq_mt4c_membership_actor");
        modelBuilder.Entity<StudentEnrollment>().Property<long>("Revision").HasColumnName("revision").HasColumnType("bigint(20)").HasDefaultValue(0L).IsConcurrencyToken();
        modelBuilder.Entity<CourseAdministrationEvent>(entity =>
        {
            entity.HasKey(e => new { e.InstitutionId, e.Id })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("course_administration_event")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ActorUserId, "fk_mt4c_actor");

            entity.HasIndex(e => new { e.InstitutionId, e.CampusId }, "fk_mt4c_campus");

            entity.HasIndex(e => new { e.InstitutionId, e.ClassId }, "fk_mt4c_class");

            entity.HasIndex(e => new { e.InstitutionId, e.ActorMembershipId, e.ActorUserId }, "fk_mt4c_membership");

            entity.HasIndex(e => new { e.InstitutionId, e.CourseEditionId, e.OccurredAtUtc, e.Id }, "idx_mt4c_edition_history");

            entity.HasIndex(e => new { e.InstitutionId, e.EnrollmentId, e.RevisionAfter, e.OccurredAtUtc, e.Id }, "idx_mt4c_enrollment_history");

            entity.Property(e => e.InstitutionId)
                .HasColumnName("institution_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.Action)
                .HasMaxLength(64)
                .HasColumnName("action")
                .UseCollation("ascii_bin")
                .HasCharSet("ascii");
            entity.Property(e => e.ActorMembershipId)
                .HasColumnName("actor_membership_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.ActorUserId)
                .HasColumnName("actor_user_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.AfterJson).HasColumnName("after_json");
            entity.Property(e => e.BeforeJson).HasColumnName("before_json");
            entity.Property(e => e.CampusId)
                .HasColumnName("campus_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.ClassId)
                .HasColumnName("class_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.CourseEditionId)
                .HasColumnName("course_edition_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.EnrollmentId)
                .HasColumnName("enrollment_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.NewCompletionDate).HasColumnName("new_completion_date");
            entity.Property(e => e.NewStatus)
                .HasMaxLength(20)
                .HasColumnName("new_status")
                .UseCollation("ascii_bin")
                .HasCharSet("ascii");
            entity.Property(e => e.OccurredAtUtc)
                .HasMaxLength(6)
                .HasColumnName("occurred_at_utc");
            entity.Property(e => e.PreviousCompletionDate).HasColumnName("previous_completion_date");
            entity.Property(e => e.PreviousStatus)
                .HasMaxLength(20)
                .HasColumnName("previous_status")
                .UseCollation("ascii_bin")
                .HasCharSet("ascii");
            entity.Property(e => e.Reason)
                .HasMaxLength(1000)
                .HasColumnName("reason");
            entity.Property(e => e.RequestHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasColumnName("request_hash")
                .UseCollation("ascii_bin")
                .HasCharSet("ascii");
            entity.Property(e => e.RevisionAfter)
                .HasColumnType("bigint(20)")
                .HasColumnName("revision_after");
            entity.Property(e => e.RevisionBefore)
                .HasColumnType("bigint(20)")
                .HasColumnName("revision_before");

            entity.HasOne(d => d.ActorUser).WithMany()
                .HasForeignKey(d => d.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_mt4c_actor");

            entity.HasOne(d => d.Campus).WithMany()
                .HasPrincipalKey(p => new { p.InstitutionId, p.Id })
                .HasForeignKey(d => new { d.InstitutionId, d.CampusId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_mt4c_campus");

            entity.Ignore(e => e.Class);

            entity.HasOne(d => d.CourseEdition).WithMany()
                .HasPrincipalKey(p => new { p.InstitutionId, p.Id })
                .HasForeignKey(d => new { d.InstitutionId, d.CourseEditionId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_mt4c_edition");

            entity.HasOne(d => d.StudentEnrollment).WithMany()
                .HasPrincipalKey(p => new { p.InstitutionId, p.Id })
                .HasForeignKey(d => new { d.InstitutionId, d.EnrollmentId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_mt4c_enrollment");

            entity.HasOne(d => d.InstitutionMembership).WithMany()
                .HasPrincipalKey(p => new { p.InstitutionId, p.Id, p.UserId })
                .HasForeignKey(d => new { d.InstitutionId, d.ActorMembershipId, d.ActorUserId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_mt4c_membership");
        });
    }
}
