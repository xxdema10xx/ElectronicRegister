using System;
using System.Collections.Generic;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class Mt3CandidateContext : DbContext
{
    public Mt3CandidateContext(DbContextOptions<Mt3CandidateContext> options)
        : base(options)
    {
    }

    public virtual DbSet<InstitutionMembership> InstitutionMemberships { get; set; }

    public virtual DbSet<InstitutionStudentProfile> InstitutionStudentProfiles { get; set; }

    public virtual DbSet<InstitutionTeacherProfile> InstitutionTeacherProfiles { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<Teacher> Teachers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<InstitutionMembership>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("institution_membership")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.InstitutionId, "idx_membership_institution");

            entity.HasIndex(e => new { e.InstitutionId, e.Id }, "uq_membership_institution_id").IsUnique();

            entity.HasIndex(e => new { e.UserId, e.InstitutionId }, "uq_membership_user_institution").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.InstitutionId)
                .HasColumnName("institution_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.JoinedAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("joined_at");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'pending'")
                .HasColumnType("enum('pending','active','suspended','left')")
                .HasColumnName("status");
            entity.Property(e => e.SuspendedAt)
                .HasMaxLength(6)
                .HasColumnName("suspended_at");
            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
        });

        modelBuilder.Entity<InstitutionStudentProfile>(entity =>
        {
            entity.HasKey(e => new { e.InstitutionId, e.MembershipId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("institution_student_profile")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.StudentId, "uq_institution_student_profile_student").IsUnique();

            entity.Property(e => e.InstitutionId)
                .HasColumnName("institution_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.MembershipId)
                .HasColumnName("membership_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.StudentId)
                .HasColumnName("student_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");

            entity.HasOne(d => d.Student).WithOne(p => p.InstitutionStudentProfile)
                .HasForeignKey<InstitutionStudentProfile>(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_student_profile_student");

            entity.HasOne(d => d.InstitutionMembership).WithOne(p => p.InstitutionStudentProfile)
                .HasPrincipalKey<InstitutionMembership>(p => new { p.InstitutionId, p.Id })
                .HasForeignKey<InstitutionStudentProfile>(d => new { d.InstitutionId, d.MembershipId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_student_profile_membership");
        });

        modelBuilder.Entity<InstitutionTeacherProfile>(entity =>
        {
            entity.HasKey(e => new { e.InstitutionId, e.MembershipId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("institution_teacher_profile")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.TeacherId, "uq_institution_teacher_profile_teacher").IsUnique();

            entity.Property(e => e.InstitutionId)
                .HasColumnName("institution_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.MembershipId)
                .HasColumnName("membership_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.TeacherId)
                .HasColumnName("teacher_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");

            entity.HasOne(d => d.Teacher).WithOne(p => p.InstitutionTeacherProfile)
                .HasForeignKey<InstitutionTeacherProfile>(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_teacher_profile_teacher");

            entity.HasOne(d => d.InstitutionMembership).WithOne(p => p.InstitutionTeacherProfile)
                .HasPrincipalKey<InstitutionMembership>(p => new { p.InstitutionId, p.Id })
                .HasForeignKey<InstitutionTeacherProfile>(d => new { d.InstitutionId, d.MembershipId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_teacher_profile_membership");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("student")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.ClassId, "idx_student_class");

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.ClassId)
                .HasColumnName("class_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasColumnName("last_name");
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("teacher")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasColumnName("last_name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
