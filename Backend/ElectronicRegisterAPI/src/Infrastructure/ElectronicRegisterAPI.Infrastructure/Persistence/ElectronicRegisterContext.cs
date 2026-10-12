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

    public virtual DbSet<AuditEvent> AuditEvents { get; set; }

    public virtual DbSet<Biennium> Biennia { get; set; }

    public virtual DbSet<BienniumStudyArea> BienniumStudyAreas { get; set; }

    public virtual DbSet<BienniumStudyPath> BienniumStudyPaths { get; set; }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<ClassSubject> ClassSubjects { get; set; }

    public virtual DbSet<Exam> Exams { get; set; }

    public virtual DbSet<ExamStudent> ExamStudents { get; set; }

    public virtual DbSet<ExamSubmission> ExamSubmissions { get; set; }

    public virtual DbSet<ExamViolation> ExamViolations { get; set; }

    public virtual DbSet<Grade> Grades { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<QuestionOption> QuestionOptions { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<StudyArea> StudyAreas { get; set; }

    public virtual DbSet<StudyPath> StudyPaths { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<SubmissionAnswer> SubmissionAnswers { get; set; }

    public virtual DbSet<Teacher> Teachers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("audit_event");

            entity.HasIndex(e => new { e.ActorUserId, e.CreatedAt }, "idx_audit_actor");

            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.CreatedAt }, "idx_audit_entity");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.AfterJson).HasColumnName("after_json");
            entity.Property(e => e.BeforeJson).HasColumnName("before_json");
            entity.Property(e => e.CreatedAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("created_at");
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .HasColumnName("entity_type");
            entity.Property(e => e.RequestId)
                .HasMaxLength(100)
                .HasColumnName("request_id");

            entity.HasOne(d => d.ActorUser).WithMany(p => p.AuditEvents)
                .HasForeignKey(d => d.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_audit_actor");
        });

        modelBuilder.Entity<Biennium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("biennium");

            entity.HasIndex(e => new { e.StartYear, e.EndYear }, "uq_biennium").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EndYear).HasColumnName("end_year");
            entity.Property(e => e.StartYear).HasColumnName("start_year");
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

        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("exam");

            entity.HasIndex(e => e.ClassSubjectId, "idx_exam_class_subject");

            entity.HasIndex(e => e.RetakeOfExamId, "idx_exam_retake_of");

            entity.HasIndex(e => new { e.Status, e.AvailableFrom, e.AvailableUntil }, "idx_exam_status_window");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AvailableFrom)
                .HasMaxLength(6)
                .HasColumnName("available_from");
            entity.Property(e => e.AvailableUntil)
                .HasMaxLength(6)
                .HasColumnName("available_until");
            entity.Property(e => e.CancelledAt)
                .HasMaxLength(6)
                .HasColumnName("cancelled_at");
            entity.Property(e => e.ClassSubjectId).HasColumnName("class_subject_id");
            entity.Property(e => e.ClosedAt)
                .HasMaxLength(6)
                .HasColumnName("closed_at");
            entity.Property(e => e.CreatedAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasColumnType("text")
                .HasColumnName("description");
            entity.Property(e => e.DurationMinutes)
                .HasColumnType("int(11)")
                .HasColumnName("duration_minutes");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.PassingPercent)
                .HasPrecision(5, 2)
                .HasDefaultValueSql("'60.00'")
                .HasColumnName("passing_percent");
            entity.Property(e => e.PublishedAt)
                .HasMaxLength(6)
                .HasColumnName("published_at");
            entity.Property(e => e.RetakeOfExamId).HasColumnName("retake_of_exam_id");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'draft'")
                .HasColumnType("enum('draft','published','closed','cancelled')")
                .HasColumnName("status");
            entity.Property(e => e.TotalPoints)
                .HasPrecision(7, 2)
                .HasColumnName("total_points");
            entity.Property(e => e.UpdatedAt)
                .HasMaxLength(6)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ClassSubject).WithMany(p => p.Exams)
                .HasForeignKey(d => d.ClassSubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_exam_class_subject");

            entity.HasOne(d => d.RetakeOfExam).WithMany(p => p.InverseRetakeOfExam)
                .HasForeignKey(d => d.RetakeOfExamId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_exam_retake_of");
        });

        modelBuilder.Entity<ExamStudent>(entity =>
        {
            entity.HasKey(e => new { e.ExamId, e.StudentId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.ToTable("exam_student");

            entity.HasIndex(e => new { e.StudentId, e.ExamId }, "idx_exam_student_student");

            entity.Property(e => e.ExamId).HasColumnName("exam_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.AssignedAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("assigned_at");
            entity.Property(e => e.ExtraTimeMinutes)
                .HasColumnType("int(11)")
                .HasColumnName("extra_time_minutes");

            entity.HasOne(d => d.Exam).WithMany(p => p.ExamStudents)
                .HasForeignKey(d => d.ExamId)
                .HasConstraintName("fk_exam_student_exam");

            entity.HasOne(d => d.Student).WithMany(p => p.ExamStudents)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_exam_student_student");
        });

        modelBuilder.Entity<ExamSubmission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("exam_submission");

            entity.HasIndex(e => e.ReviewedByUserId, "fk_exam_submission_reviewer");

            entity.HasIndex(e => new { e.ExamId, e.Status }, "idx_exam_submission_exam_status");

            entity.HasIndex(e => new { e.Status, e.DeadlineAt }, "idx_exam_submission_expiration");

            entity.HasIndex(e => new { e.StudentId, e.Status }, "idx_exam_submission_student_status");

            entity.HasIndex(e => e.GradeId, "uq_exam_submission_grade").IsUnique();

            entity.HasIndex(e => new { e.ExamId, e.StudentId }, "uq_exam_submission_student").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClosedScore)
                .HasPrecision(7, 2)
                .HasColumnName("closed_score");
            entity.Property(e => e.CreatedAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("created_at");
            entity.Property(e => e.DeadlineAt)
                .HasMaxLength(6)
                .HasColumnName("deadline_at");
            entity.Property(e => e.ExamId).HasColumnName("exam_id");
            entity.Property(e => e.FinalizedAt)
                .HasMaxLength(6)
                .HasColumnName("finalized_at");
            entity.Property(e => e.GradeId).HasColumnName("grade_id");
            entity.Property(e => e.LastClientRevision)
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("last_client_revision");
            entity.Property(e => e.MaxScore)
                .HasPrecision(7, 2)
                .HasColumnName("max_score");
            entity.Property(e => e.OpenMaxPoints)
                .HasPrecision(7, 2)
                .HasColumnName("open_max_points");
            entity.Property(e => e.OpenScore)
                .HasPrecision(7, 2)
                .HasColumnName("open_score");
            entity.Property(e => e.Passed).HasColumnName("passed");
            entity.Property(e => e.PassingScore)
                .HasPrecision(7, 2)
                .HasColumnName("passing_score");
            entity.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
            entity.Property(e => e.StartedAt)
                .HasMaxLength(6)
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'in_progress'")
                .HasColumnType("enum('in_progress','graded','pending_review','failed_auto','finalized')")
                .HasColumnName("status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.SubmitReason)
                .HasColumnType("enum('manual','time_expired','violations')")
                .HasColumnName("submit_reason");
            entity.Property(e => e.SubmittedAt)
                .HasMaxLength(6)
                .HasColumnName("submitted_at");
            entity.Property(e => e.SuggestedGrade)
                .HasPrecision(4, 2)
                .HasColumnName("suggested_grade");
            entity.Property(e => e.TotalScore)
                .HasPrecision(7, 2)
                .HasColumnName("total_score");
            entity.Property(e => e.UpdatedAt)
                .HasMaxLength(6)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("updated_at");
            entity.Property(e => e.ViolationCount)
                .HasColumnType("int(11)")
                .HasColumnName("violation_count");

            entity.HasOne(d => d.Exam).WithMany(p => p.ExamSubmissions)
                .HasForeignKey(d => d.ExamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_exam_submission_exam");

            entity.HasOne(d => d.Grade).WithOne(p => p.ExamSubmission)
                .HasForeignKey<ExamSubmission>(d => d.GradeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_exam_submission_grade");

            entity.HasOne(d => d.ReviewedByUser).WithMany(p => p.ExamSubmissions)
                .HasForeignKey(d => d.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_exam_submission_reviewer");

            entity.HasOne(d => d.Student).WithMany(p => p.ExamSubmissions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_exam_submission_student");
        });

        modelBuilder.Entity<ExamViolation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("exam_violation");

            entity.HasIndex(e => new { e.SubmissionId, e.OccurredAt }, "idx_exam_violation_submission");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MetadataJson).HasColumnName("metadata_json");
            entity.Property(e => e.OccurredAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("occurred_at");
            entity.Property(e => e.SubmissionId).HasColumnName("submission_id");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");

            entity.HasOne(d => d.Submission).WithMany(p => p.ExamViolations)
                .HasForeignKey(d => d.SubmissionId)
                .HasConstraintName("fk_exam_violation_submission");
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

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("question");

            entity.HasIndex(e => e.ExamId, "idx_question_exam");

            entity.HasIndex(e => new { e.ExamId, e.Position }, "uq_question_position").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ExamId).HasColumnName("exam_id");
            entity.Property(e => e.Points)
                .HasPrecision(7, 2)
                .HasColumnName("points");
            entity.Property(e => e.Position)
                .HasColumnType("int(11)")
                .HasColumnName("position");
            entity.Property(e => e.Required).HasColumnName("required");
            entity.Property(e => e.Text)
                .HasColumnType("text")
                .HasColumnName("text");
            entity.Property(e => e.Type)
                .HasColumnType("enum('single','multiple','open')")
                .HasColumnName("type");

            entity.HasOne(d => d.Exam).WithMany(p => p.Questions)
                .HasForeignKey(d => d.ExamId)
                .HasConstraintName("fk_question_exam");
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("question_option");

            entity.HasIndex(e => e.QuestionId, "idx_question_option_question");

            entity.HasIndex(e => new { e.QuestionId, e.Position }, "uq_question_option_position").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IsCorrect).HasColumnName("is_correct");
            entity.Property(e => e.Position)
                .HasColumnType("int(11)")
                .HasColumnName("position");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.Text)
                .HasMaxLength(500)
                .HasColumnName("text");

            entity.HasOne(d => d.Question).WithMany(p => p.QuestionOptions)
                .HasForeignKey(d => d.QuestionId)
                .HasConstraintName("fk_question_option_question");
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

        modelBuilder.Entity<SubmissionAnswer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("submission_answer");

            entity.HasIndex(e => e.QuestionId, "idx_submission_answer_question");

            entity.HasIndex(e => e.SubmissionId, "idx_submission_answer_submission");

            entity.HasIndex(e => new { e.SubmissionId, e.QuestionId }, "uq_submission_answer").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AwardedPoints)
                .HasPrecision(7, 2)
                .HasColumnName("awarded_points");
            entity.Property(e => e.IsCorrect).HasColumnName("is_correct");
            entity.Property(e => e.LastSavedAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("last_saved_at");
            entity.Property(e => e.OpenText)
                .HasColumnType("text")
                .HasColumnName("open_text");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");
            entity.Property(e => e.SubmissionId).HasColumnName("submission_id");
            entity.Property(e => e.TeacherComment)
                .HasColumnType("text")
                .HasColumnName("teacher_comment");

            entity.HasOne(d => d.Question).WithMany(p => p.SubmissionAnswers)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_submission_answer_question");

            entity.HasOne(d => d.Submission).WithMany(p => p.SubmissionAnswers)
                .HasForeignKey(d => d.SubmissionId)
                .HasConstraintName("fk_submission_answer_submission");

            entity.HasMany(d => d.Options).WithMany(p => p.SubmissionAnswers)
                .UsingEntity<Dictionary<string, object>>(
                    "SubmissionAnswerOption",
                    r => r.HasOne<QuestionOption>().WithMany()
                        .HasForeignKey("OptionId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_submission_answer_option_option"),
                    l => l.HasOne<SubmissionAnswer>().WithMany()
                        .HasForeignKey("SubmissionAnswerId")
                        .HasConstraintName("fk_submission_answer_option_answer"),
                    j =>
                    {
                        j.HasKey("SubmissionAnswerId", "OptionId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.ToTable("submission_answer_option");
                        j.HasIndex(new[] { "OptionId" }, "idx_submission_answer_option_option");
                        j.IndexerProperty<Guid>("SubmissionAnswerId").HasColumnName("submission_answer_id");
                        j.IndexerProperty<Guid>("OptionId").HasColumnName("option_id");
                    });
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
