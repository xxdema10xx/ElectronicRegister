// Database-first mapping from migrations/mt3/001_profile_expand.sql. Regenerate with Scaffold-Mt3.ps1.
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    public virtual DbSet<InstitutionStudentProfile> InstitutionStudentProfiles { get; set; }
    public virtual DbSet<InstitutionTeacherProfile> InstitutionTeacherProfiles { get; set; }

    private static void ConfigureMt3Profiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstitutionStudentProfile>(entity =>
        {
            entity.HasKey(e => new { e.InstitutionId, e.MembershipId }).HasName("PRIMARY").HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
            entity.ToTable("institution_student_profile").UseCollation("utf8mb4_unicode_ci");
            entity.HasIndex(e => e.StudentId, "uq_institution_student_profile_student").IsUnique();
            entity.Property(e => e.InstitutionId).HasColumnName("institution_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.MembershipId).HasColumnName("membership_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.StudentId).HasColumnName("student_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.HasOne(d => d.Student).WithOne().HasForeignKey<InstitutionStudentProfile>(d => d.StudentId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("fk_student_profile_student");
            entity.HasOne(d => d.InstitutionMembership).WithOne().HasPrincipalKey<InstitutionMembership>(p => new { p.InstitutionId, p.Id }).HasForeignKey<InstitutionStudentProfile>(d => new { d.InstitutionId, d.MembershipId }).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("fk_student_profile_membership");
        });
        modelBuilder.Entity<InstitutionTeacherProfile>(entity =>
        {
            entity.HasKey(e => new { e.InstitutionId, e.MembershipId }).HasName("PRIMARY").HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
            entity.ToTable("institution_teacher_profile").UseCollation("utf8mb4_unicode_ci");
            entity.HasIndex(e => e.TeacherId, "uq_institution_teacher_profile_teacher").IsUnique();
            entity.Property(e => e.InstitutionId).HasColumnName("institution_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.MembershipId).HasColumnName("membership_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.HasOne(d => d.Teacher).WithOne().HasForeignKey<InstitutionTeacherProfile>(d => d.TeacherId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("fk_teacher_profile_teacher");
            entity.HasOne(d => d.InstitutionMembership).WithOne().HasPrincipalKey<InstitutionMembership>(p => new { p.InstitutionId, p.Id }).HasForeignKey<InstitutionTeacherProfile>(d => new { d.InstitutionId, d.MembershipId }).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("fk_teacher_profile_membership");
        });
    }
}
