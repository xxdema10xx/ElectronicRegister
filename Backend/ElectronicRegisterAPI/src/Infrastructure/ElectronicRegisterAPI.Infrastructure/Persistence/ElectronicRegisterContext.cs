using System;
using System.Collections.Generic;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext : DbContext
{
    public ElectronicRegisterContext(DbContextOptions<ElectronicRegisterContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Biennium> Biennia { get; set; }

    public virtual DbSet<BienniumStudyArea> BienniumStudyAreas { get; set; }

    public virtual DbSet<BienniumStudyPath> BienniumStudyPaths { get; set; }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<ClassSubject> ClassSubjects { get; set; }

    public virtual DbSet<Grade> Grades { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<StudyArea> StudyAreas { get; set; }

    public virtual DbSet<StudyPath> StudyPaths { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<Teacher> Teachers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Biennium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("biennium");

            entity.HasIndex(e => new { e.StartYear, e.EndYear }, "uq_biennium").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EndYear)
                .HasColumnType("int(11)")
                .HasColumnName("end_year");
            entity.Property(e => e.Name)
                .HasMaxLength(20)
                .HasColumnName("name");
            entity.Property(e => e.StartYear)
                .HasColumnType("int(11)")
                .HasColumnName("start_year");
        });

        modelBuilder.Entity<BienniumStudyArea>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("biennium_study_area");

            entity.HasIndex(e => e.StudyAreaId, "idx_bsa_area");

            entity.HasIndex(e => new { e.BienniumId, e.StudyAreaId }, "uq_biennium_area").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BienniumId).HasColumnName("biennium_id");
            entity.Property(e => e.StudyAreaId).HasColumnName("study_area_id");

            entity.HasOne(d => d.Biennium).WithMany(p => p.BienniumStudyAreas)
                .HasForeignKey(d => d.BienniumId)
                .HasConstraintName("fk_bsa_biennium");

            entity.HasOne(d => d.StudyArea).WithMany(p => p.BienniumStudyAreas)
                .HasForeignKey(d => d.StudyAreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_bsa_area");
        });

        modelBuilder.Entity<BienniumStudyPath>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("biennium_study_path");

            entity.HasIndex(e => e.StudyPathId, "idx_bsp_path");

            entity.HasIndex(e => new { e.BienniumStudyAreaId, e.StudyPathId }, "uq_bsa_path").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BienniumStudyAreaId).HasColumnName("biennium_study_area_id");
            entity.Property(e => e.StudyPathId).HasColumnName("study_path_id");

            entity.HasOne(d => d.BienniumStudyArea).WithMany(p => p.BienniumStudyPaths)
                .HasForeignKey(d => d.BienniumStudyAreaId)
                .HasConstraintName("fk_bsp_bsa");

            entity.HasOne(d => d.StudyPath).WithMany(p => p.BienniumStudyPaths)
                .HasForeignKey(d => d.StudyPathId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_bsp_path");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("class");

            entity.HasIndex(e => e.BienniumStudyPathId, "idx_class_bsp");

            entity.HasIndex(e => new { e.BienniumStudyPathId, e.Name }, "uq_class").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BienniumStudyPathId).HasColumnName("biennium_study_path_id");
            entity.Property(e => e.Name)
                .HasMaxLength(20)
                .HasColumnName("name");

            entity.HasOne(d => d.BienniumStudyPath).WithMany(p => p.Classes)
                .HasForeignKey(d => d.BienniumStudyPathId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_class_bsp");
        });

        modelBuilder.Entity<ClassSubject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("class_subject");

            entity.HasIndex(e => e.SubjectId, "idx_cs_subject");

            entity.HasIndex(e => e.TeacherId, "idx_cs_teacher");

            entity.HasIndex(e => new { e.ClassId, e.SubjectId }, "uq_class_subject").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassSubjects)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("fk_cs_class");

            entity.HasOne(d => d.Subject).WithMany(p => p.ClassSubjects)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cs_subject");

            entity.HasOne(d => d.Teacher).WithMany(p => p.ClassSubjects)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cs_teacher");
        });

        modelBuilder.Entity<Grade>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("grade");

            entity.HasIndex(e => e.ClassSubjectId, "idx_grade_class_subject");

            entity.HasIndex(e => e.StudentId, "idx_grade_student");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClassSubjectId).HasColumnName("class_subject_id");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.Value)
                .HasPrecision(4, 2)
                .HasColumnName("value");

            entity.HasOne(d => d.ClassSubject).WithMany(p => p.Grades)
                .HasForeignKey(d => d.ClassSubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_grade_class_subject");

            entity.HasOne(d => d.Student).WithMany(p => p.Grades)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("fk_grade_student");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("student");

            entity.HasIndex(e => e.ClassId, "idx_student_class");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasColumnName("last_name");

            entity.HasOne(d => d.Class).WithMany(p => p.Students)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_student_class");
        });

        modelBuilder.Entity<StudyArea>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("study_area");

            entity.HasIndex(e => e.Name, "uq_study_area_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<StudyPath>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("study_path");

            entity.HasIndex(e => e.Name, "uq_study_path_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("subject");

            entity.HasIndex(e => e.Name, "uq_subject_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("teacher");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .HasColumnName("first_name");
            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .HasColumnName("last_name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("user");

            entity.HasIndex(e => e.StudentId, "idx_user_student").IsUnique();

            entity.HasIndex(e => e.TeacherId, "idx_user_teacher").IsUnique();

            entity.HasIndex(e => e.Email, "uq_user_email").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Role)
                .HasColumnType("enum('admin','teacher','student')")
                .HasColumnName("role");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.TeacherId).HasColumnName("teacher_id");

            entity.HasOne(d => d.Student).WithOne(p => p.User)
                .HasForeignKey<User>(d => d.StudentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_student");

            entity.HasOne(d => d.Teacher).WithOne(p => p.User)
                .HasForeignKey<User>(d => d.TeacherId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_teacher");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
