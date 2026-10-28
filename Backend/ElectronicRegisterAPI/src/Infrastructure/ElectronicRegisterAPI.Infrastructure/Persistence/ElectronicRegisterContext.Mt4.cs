// MT4 database-first sidecar. Reproduced by migrations/mt4/Reconcile-Mt4.py.
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    public virtual DbSet<CourseEdition> CourseEditions { get; set; }
    public virtual DbSet<CourseEditionCampus> CourseEditionCampuses { get; set; }
    public virtual DbSet<StudentEnrollment> StudentEnrollments { get; set; }

    private static void ConfigureMt4(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseEdition>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("course_edition").UseCollation("utf8mb4_unicode_ci");
            entity.HasIndex(e => new { e.InstitutionId, e.StudyPathId }, "idx_mt4_edition_path");
            entity.HasIndex(e => new { e.InstitutionId, e.Code }, "uq_mt4_edition_code").IsUnique();
            entity.HasIndex(e => new { e.InstitutionId, e.Id }, "uq_mt4_edition_owner").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.Code).HasMaxLength(64).HasColumnName("code");
            entity.Property(e => e.CreatedAt).HasMaxLength(6).HasDefaultValueSql("current_timestamp(6)").HasColumnName("created_at");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.InstitutionId).HasColumnName("institution_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.Name).HasMaxLength(200).HasColumnName("name");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status").UseCollation("ascii_bin").HasCharSet("ascii");
            entity.Property(e => e.StudyPathId).HasColumnName("study_path_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.UpdatedAt).HasMaxLength(6).ValueGeneratedOnAddOrUpdate().HasDefaultValueSql("current_timestamp(6)").HasColumnName("updated_at");
            entity.HasOne(d => d.Institution).WithMany().HasForeignKey(d => d.InstitutionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mt4_edition_owner");
            entity.HasOne(d => d.StudyPath).WithMany().HasPrincipalKey("InstitutionId", "Id").HasForeignKey(d => new { d.InstitutionId, d.StudyPathId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mt4_edition_path");
        });
        modelBuilder.Entity<CourseEditionCampus>(entity =>
        {
            entity.HasKey(e => new { e.InstitutionId, e.CourseEditionId, e.CampusId }).HasName("PRIMARY").HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });
            entity.ToTable("course_edition_campus").UseCollation("utf8mb4_unicode_ci");
            entity.HasIndex(e => new { e.InstitutionId, e.CampusId }, "idx_mt4_edition_campus");
            entity.Property(e => e.InstitutionId).HasColumnName("institution_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.CourseEditionId).HasColumnName("course_edition_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.CampusId).HasColumnName("campus_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.HasOne(d => d.Campus).WithMany().HasPrincipalKey(p => new { p.InstitutionId, p.Id }).HasForeignKey(d => new { d.InstitutionId, d.CampusId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mt4_ec_campus");
            entity.HasOne(d => d.CourseEdition).WithMany(p => p.CourseEditionCampuses).HasPrincipalKey(p => new { p.InstitutionId, p.Id }).HasForeignKey(d => new { d.InstitutionId, d.CourseEditionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mt4_ec_edition");
        });
        modelBuilder.Entity<StudentEnrollment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("student_enrollment").UseCollation("utf8mb4_unicode_ci");
            entity.HasIndex(e => new { e.InstitutionId, e.CourseEditionId }, "idx_mt4_enrollment_edition");
            entity.HasIndex(e => new { e.InstitutionId, e.Id }, "uq_mt4_enrollment_owner").IsUnique();
            entity.HasIndex(e => new { e.InstitutionId, e.StudentId, e.CourseEditionId }, "uq_mt4_enrollment_student").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.CompletionDate).HasColumnName("completion_date");
            entity.Property(e => e.CourseEditionId).HasColumnName("course_edition_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.CreatedAt).HasMaxLength(6).HasDefaultValueSql("current_timestamp(6)").HasColumnName("created_at");
            entity.Property(e => e.EnrollmentDate).HasColumnName("enrollment_date");
            entity.Property(e => e.InstitutionId).HasColumnName("institution_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status").UseCollation("ascii_bin").HasCharSet("ascii");
            entity.Property(e => e.StudentId).HasColumnName("student_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.UpdatedAt).HasMaxLength(6).ValueGeneratedOnAddOrUpdate().HasDefaultValueSql("current_timestamp(6)").HasColumnName("updated_at");
            entity.HasOne(d => d.CourseEdition).WithMany(p => p.StudentEnrollments).HasPrincipalKey(p => new { p.InstitutionId, p.Id }).HasForeignKey(d => new { d.InstitutionId, d.CourseEditionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mt4_enrollment_edition");
            entity.HasOne(d => d.Student).WithMany().HasPrincipalKey("InstitutionId", "Id").HasForeignKey(d => new { d.InstitutionId, d.StudentId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mt4_enrollment_student");
        });
        modelBuilder.Entity<Class>().Property<Guid?>("CourseEditionId").HasColumnName("course_edition_id").HasColumnType("char(36)").UseCollation("ascii_general_ci").HasCharSet("ascii");
        modelBuilder.Entity<Class>().HasIndex("InstitutionId", "CourseEditionId").HasDatabaseName("idx_mt4_class_edition");
        modelBuilder.Entity<Class>().HasOne<CourseEdition>().WithMany(e => e.Classes).HasForeignKey("InstitutionId", "CourseEditionId").HasPrincipalKey(e => new { e.InstitutionId, e.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mt4_class_edition");
        modelBuilder.Entity<Campus>().HasAlternateKey(e => new { e.InstitutionId, e.Id }).HasName("uq_mt4_campus_owner");
        modelBuilder.Entity<StudyPath>().HasAlternateKey("InstitutionId", "Id").HasName("uq_mt3a_study_path_owner");
        modelBuilder.Entity<Student>().HasAlternateKey("InstitutionId", "Id").HasName("uq_mt3a_student_owner");
        // Alternate-key shadow owners must not receive a permanent client Guid.
        // Temporary values are omitted from INSERT; MT3A SQL derives the real owner.
        modelBuilder.Entity<StudyPath>().Property<Guid>("InstitutionId").HasValueGenerator<Microsoft.EntityFrameworkCore.ValueGeneration.TemporaryGuidValueGenerator>();
        modelBuilder.Entity<Student>().Property<Guid>("InstitutionId").HasValueGenerator<Microsoft.EntityFrameworkCore.ValueGeneration.TemporaryGuidValueGenerator>();
        modelBuilder.Entity<CourseEdition>().Property(e => e.InstitutionId).IsConcurrencyToken();
        modelBuilder.Entity<StudentEnrollment>().Property(e => e.InstitutionId).IsConcurrencyToken();
        modelBuilder.Entity<Class>().Property<Guid?>("CourseEditionId").IsConcurrencyToken();
    }
}
