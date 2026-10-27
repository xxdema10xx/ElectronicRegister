// Database First sidecar derived from migrations/institution-email/001_expand.sql.
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Infrastructure.Persistence;
public partial class ElectronicRegisterContext
{
 private static void ConfigureInstitutionEmails(ModelBuilder b)
 {
  { var e=b.Entity<InstitutionEmailMutex>(); e.ToTable("institution_email_mutex");
   e.HasKey(x=>new {x.Id});
   e.Property(x=>x.Id).HasColumnName("id").HasColumnType("int").IsRequired();
  }
  { var e=b.Entity<InstitutionEmailPolicy>(); e.ToTable("institution_email_policy");
   e.HasKey(x=>new {x.InstitutionId,x.Category,x.Revision});
   e.Property(x=>x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.PolicyId).HasColumnName("policy_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.Category).HasColumnName("category").HasColumnType("varchar(16)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
   e.Property(x=>x.Revision).HasColumnName("revision").HasColumnType("bigint").IsRequired();
   e.Property(x=>x.State).HasColumnName("state").HasColumnType("varchar(10)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
   e.Property(x=>x.Configuration).HasColumnName("configuration").HasColumnType("json").IsRequired();
   e.Property(x=>x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
   e.HasIndex(x=>new {x.InstitutionId,x.PolicyId,x.Revision}).IsUnique().HasDatabaseName("uq_email_policy_version");
   e.HasIndex(x=>new {x.InstitutionId,x.ActorMembershipId,x.ActorUserId}).HasDatabaseName("fk_email_policy_actor");
   e.HasOne<Institution>().WithMany().HasForeignKey(x=>new {x.InstitutionId}).HasPrincipalKey(x=>new {x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_policy_institution");
   e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x=>new {x.InstitutionId,x.ActorMembershipId,x.ActorUserId}).HasPrincipalKey(x=>new {x.InstitutionId,x.Id,x.UserId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_policy_actor");
  }
  { var e=b.Entity<InstitutionEmailAssignment>(); e.ToTable("institution_email_assignment");
   e.HasKey(x=>new {x.Id});
   e.Property(x=>x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.StudentId).HasColumnName("student_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
   e.Property(x=>x.TeacherId).HasColumnName("teacher_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
   e.Property(x=>x.MembershipId).HasColumnName("membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
   e.Property(x=>x.Category).HasColumnName("category").HasColumnType("varchar(16)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
   e.Property(x=>x.PolicyRevision).HasColumnName("policy_revision").HasColumnType("bigint").IsRequired();
   e.Property(x=>x.Email).HasColumnName("email").HasColumnType("varchar(254)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.State).HasColumnName("state").HasColumnType("enum('reserved','provisioned','verified','retired')").IsRequired();
   e.Property(x=>x.Revision).HasColumnName("revision").HasColumnType("bigint").IsRequired();
   e.Property(x=>x.FirstNameSnapshot).HasColumnName("first_name_snapshot").HasColumnType("varchar(256)").HasCharSet("utf8mb4").UseCollation("utf8mb4_unicode_ci").IsRequired();
   e.Property(x=>x.LastNameSnapshot).HasColumnName("last_name_snapshot").HasColumnType("varchar(256)").HasCharSet("utf8mb4").UseCollation("utf8mb4_unicode_ci").IsRequired();
   e.Property(x=>x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();
   e.Property(x=>x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();
   e.HasIndex(x=>new {x.InstitutionId,x.Id}).IsUnique().HasDatabaseName("uq_email_assignment_owner");
   e.HasIndex(x=>new {x.InstitutionId,x.StudentId}).IsUnique().HasDatabaseName("uq_email_assignment_student");
   e.HasIndex(x=>new {x.InstitutionId,x.TeacherId}).IsUnique().HasDatabaseName("uq_email_assignment_teacher");
   e.HasIndex(x=>new {x.InstitutionId,x.MembershipId}).IsUnique().HasDatabaseName("uq_email_assignment_member");
   e.HasIndex(x=>new {x.InstitutionId,x.CreatedAtUtc,x.Id}).HasDatabaseName("idx_email_assignment_page");
   e.HasIndex(x=>new {x.InstitutionId,x.ActorMembershipId,x.ActorUserId}).HasDatabaseName("fk_email_assignment_actor");
   e.HasIndex(x=>new {x.InstitutionId,x.Category,x.PolicyRevision}).HasDatabaseName("fk_email_assignment_policy");
   e.HasOne<Institution>().WithMany().HasForeignKey(x=>new {x.InstitutionId}).HasPrincipalKey(x=>new {x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_assignment_institution");
   // Composite Student/Teacher ownership FKs remain SQL-only, as in Mt3aOwnership.
   // Modeling their generated shadow tenant as an alternate EF key causes EF to
   // generate a random tenant Guid for legacy inserts. SQL enforces both FKs.
   e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x=>new {x.InstitutionId,x.MembershipId}).HasPrincipalKey(x=>new {x.InstitutionId,x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_assignment_member");
   e.HasOne<InstitutionEmailPolicy>().WithMany().HasForeignKey(x=>new {x.InstitutionId,x.Category,x.PolicyRevision}).HasPrincipalKey(x=>new {x.InstitutionId,x.Category,x.Revision}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_assignment_policy");
   e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x=>new {x.InstitutionId,x.ActorMembershipId,x.ActorUserId}).HasPrincipalKey(x=>new {x.InstitutionId,x.Id,x.UserId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_assignment_actor");
  }
  { var e=b.Entity<InstitutionEmailReservation>(); e.ToTable("institution_email_reservation");
   e.HasKey(x=>new {x.Address});
   e.Property(x=>x.Address).HasColumnName("address").HasColumnType("varchar(255)").HasCharSet("utf8mb4").UseCollation("utf8mb4_unicode_ci").IsRequired();
   e.Property(x=>x.UserId).HasColumnName("user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
   e.Property(x=>x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
   e.Property(x=>x.AssignmentId).HasColumnName("assignment_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
   e.HasIndex(x=>new {x.UserId}).HasDatabaseName("idx_email_reservation_user");
   e.HasIndex(x=>new {x.InstitutionId,x.AssignmentId}).HasDatabaseName("fk_email_reservation_assignment");
   e.HasOne<User>().WithMany().HasForeignKey(x=>new {x.UserId}).HasPrincipalKey(x=>new {x.Id}).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_email_reservation_user");
   e.HasOne<InstitutionEmailAssignment>().WithMany().HasForeignKey(x=>new {x.InstitutionId,x.AssignmentId}).HasPrincipalKey(x=>new {x.InstitutionId,x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_reservation_assignment");
  }
  { var e=b.Entity<InstitutionEmailEvent>(); e.ToTable("institution_email_event");
   e.HasKey(x=>new {x.InstitutionId,x.Id});
   e.Property(x=>x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci").IsRequired();
   e.Property(x=>x.Operation).HasColumnName("operation").HasColumnType("varchar(24)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
   e.Property(x=>x.Category).HasColumnName("category").HasColumnType("varchar(16)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
   e.Property(x=>x.PolicyRevision).HasColumnName("policy_revision").HasColumnType("bigint").IsRequired();
   e.Property(x=>x.AssignmentId).HasColumnName("assignment_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
   e.Property(x=>x.RequestHash).HasColumnName("request_hash").HasColumnType("char(64)").HasCharSet("ascii").UseCollation("ascii_bin").IsRequired();
   e.Property(x=>x.BeforeJson).HasColumnName("before_json").HasColumnType("json");
   e.Property(x=>x.AfterJson).HasColumnName("after_json").HasColumnType("json").IsRequired();
   e.Property(x=>x.Reason).HasColumnName("reason").HasColumnType("varchar(500)").HasCharSet("utf8mb4").UseCollation("utf8mb4_unicode_ci").IsRequired();
   e.Property(x=>x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime(6)").IsRequired();
   e.HasIndex(x=>new {x.InstitutionId,x.AssignmentId,x.OccurredAtUtc,x.Id}).HasDatabaseName("idx_email_event_assignment");
   e.HasIndex(x=>new {x.InstitutionId,x.Category,x.PolicyRevision}).HasDatabaseName("idx_email_event_policy");
   e.HasIndex(x=>new {x.InstitutionId,x.ActorMembershipId,x.ActorUserId}).HasDatabaseName("fk_email_event_actor");
   e.HasOne<Institution>().WithMany().HasForeignKey(x=>new {x.InstitutionId}).HasPrincipalKey(x=>new {x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_event_institution");
   e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x=>new {x.InstitutionId,x.ActorMembershipId,x.ActorUserId}).HasPrincipalKey(x=>new {x.InstitutionId,x.Id,x.UserId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_event_actor");
   e.HasOne<InstitutionEmailPolicy>().WithMany().HasForeignKey(x=>new {x.InstitutionId,x.Category,x.PolicyRevision}).HasPrincipalKey(x=>new {x.InstitutionId,x.Category,x.Revision}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_event_policy");
   e.HasOne<InstitutionEmailAssignment>().WithMany().HasForeignKey(x=>new {x.InstitutionId,x.AssignmentId}).HasPrincipalKey(x=>new {x.InstitutionId,x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_email_event_assignment");
  }
 }
}
