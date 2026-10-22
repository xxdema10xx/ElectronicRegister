using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Infrastructure.Persistence;
public partial class ElectronicRegisterContext
{
    public DbSet<StudentClassTransferEvent> StudentClassTransferEvents => Set<StudentClassTransferEvent>();
    private static void ConfigureStudentTransfers(ModelBuilder b)
    {
        b.Entity<Student>().Property<long>("TransferRevision").HasColumnName("transfer_revision").HasColumnType("bigint(20)").HasDefaultValue(0L).IsConcurrencyToken();
        var e=b.Entity<StudentClassTransferEvent>();e.ToTable("student_class_transfer_event");e.HasKey(x=>new{x.InstitutionId,x.Id});
        e.Property(x=>x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.StudentId).HasColumnName("student_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.FromClassId).HasColumnName("from_class_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.ToClassId).HasColumnName("to_class_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.FromCourseEditionId).HasColumnName("from_course_edition_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.ToCourseEditionId).HasColumnName("to_course_edition_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.SourceEnrollmentId).HasColumnName("source_enrollment_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.DestinationEnrollmentId).HasColumnName("destination_enrollment_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x=>x.EffectiveDate).HasColumnName("effective_date").HasColumnType("date");
        e.Property(x=>x.SourceCourseStartDate).HasColumnName("source_course_start_date").HasColumnType("date");
        e.Property(x=>x.DestinationCourseStartDate).HasColumnName("destination_course_start_date").HasColumnType("date");
        e.Property(x=>x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime(6)");
        e.Property(x=>x.AppliedLimitMonths).HasColumnName("applied_limit_months");
        e.Property(x=>x.RevisionBefore).HasColumnName("revision_before").HasColumnType("bigint(20)");
        e.Property(x=>x.RevisionAfter).HasColumnName("revision_after").HasColumnType("bigint(20)");
        e.Property(x=>x.Reason).HasColumnName("reason").HasMaxLength(1000);
        e.Property(x=>x.RequestHash).HasColumnName("request_hash").HasColumnType("char(64)").HasCharSet("ascii").UseCollation("ascii_bin");
        e.Property(x=>x.FromClassName).HasColumnName("from_class_name").HasMaxLength(200);
        e.Property(x=>x.ToClassName).HasColumnName("to_class_name").HasMaxLength(200);
        e.Property(x=>x.FromCourseName).HasColumnName("from_course_name").HasMaxLength(200);
        e.Property(x=>x.ToCourseName).HasColumnName("to_course_name").HasMaxLength(200);
        e.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.RevisionAfter}).IsUnique().HasDatabaseName("uq_transfer_revision");
        e.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.OccurredAtUtc,x.Id}).HasDatabaseName("idx_transfer_history");
        e.HasIndex(x=>new{x.InstitutionId,x.ActorMembershipId,x.ActorUserId}).HasDatabaseName("fk_transfer_actor");
        e.HasOne<Institution>().WithMany().HasForeignKey(x=>x.InstitutionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_owner");
        e.HasOne<Student>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.StudentId}).HasPrincipalKey("InstitutionId","Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_student");
        e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.ActorMembershipId,x.ActorUserId}).HasPrincipalKey(x=>new{x.InstitutionId,x.Id,x.UserId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_actor");
        e.HasIndex(x=>new{x.InstitutionId,x.FromClassId}).HasDatabaseName("fk_transfer_from_class");
        e.HasOne<Class>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.FromClassId}).HasPrincipalKey("InstitutionId","Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_from_class");
        e.HasIndex(x=>new{x.InstitutionId,x.ToClassId}).HasDatabaseName("fk_transfer_to_class");
        e.HasOne<Class>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.ToClassId}).HasPrincipalKey("InstitutionId","Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_to_class");
        e.HasIndex(x=>new{x.InstitutionId,x.FromCourseEditionId}).HasDatabaseName("fk_transfer_from_course");
        e.HasOne<CourseEdition>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.FromCourseEditionId}).HasPrincipalKey("InstitutionId","Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_from_course");
        e.HasIndex(x=>new{x.InstitutionId,x.ToCourseEditionId}).HasDatabaseName("fk_transfer_to_course");
        e.HasOne<CourseEdition>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.ToCourseEditionId}).HasPrincipalKey("InstitutionId","Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_to_course");
        e.HasIndex(x=>new{x.InstitutionId,x.SourceEnrollmentId}).HasDatabaseName("fk_transfer_source_enrollment");
        e.HasOne<StudentEnrollment>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.SourceEnrollmentId}).HasPrincipalKey("InstitutionId","Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_source_enrollment");
        e.HasIndex(x=>new{x.InstitutionId,x.DestinationEnrollmentId}).HasDatabaseName("fk_transfer_destination_enrollment");
        e.HasOne<StudentEnrollment>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.DestinationEnrollmentId}).HasPrincipalKey("InstitutionId","Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_transfer_destination_enrollment");
    }
}
