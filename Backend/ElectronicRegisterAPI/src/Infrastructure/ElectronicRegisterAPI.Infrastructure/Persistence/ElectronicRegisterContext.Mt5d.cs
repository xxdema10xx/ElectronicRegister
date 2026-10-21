using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    public virtual DbSet<InstitutionMembershipEvent> InstitutionMembershipEvents { get; set; }
    private static void ConfigureMt5d(ModelBuilder b)
    {
        b.Entity<InstitutionMembershipEvent>(e =>
        {
            e.ToTable("institution_membership_event").UseCollation("utf8mb4_unicode_ci");
            e.HasKey(x=>new{x.InstitutionId,x.Id});
            e.HasIndex(x=>new{x.InstitutionId,x.TargetMembershipId,x.RevisionAfter},"uq_membership_event_revision").IsUnique();
            e.HasIndex(x=>new{x.InstitutionId,x.ActorMembershipId,x.ActorUserId},"idx_membership_event_actor");
            foreach(var (property,column) in new[]{("InstitutionId","institution_id"),("Id","id"),("ActorUserId","actor_user_id"),
                ("ActorMembershipId","actor_membership_id"),("TargetMembershipId","target_membership_id")})
                e.Property<Guid>(property).HasColumnName(column).HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(x=>x.Action).HasColumnName("action").HasMaxLength(20).HasCharSet("ascii").UseCollation("ascii_bin");
            e.Property(x=>x.TargetRole).HasColumnName("target_role").HasMaxLength(20).HasCharSet("ascii").UseCollation("ascii_bin");
            e.Property(x=>x.PreviousStatus).HasColumnName("previous_status").HasMaxLength(10).HasCharSet("ascii").UseCollation("ascii_bin");
            e.Property(x=>x.NewStatus).HasColumnName("new_status").HasMaxLength(10).HasCharSet("ascii").UseCollation("ascii_bin");
            e.Property(x=>x.PreviousRoles).HasColumnName("previous_roles").HasColumnType("json");
            e.Property(x=>x.NewRoles).HasColumnName("new_roles").HasColumnType("json");
            e.Property(x=>x.Reason).HasColumnName("reason").HasMaxLength(500);
            e.Property(x=>x.RequestHash).HasColumnName("request_hash").HasColumnType("char(64)").HasCharSet("ascii").UseCollation("ascii_bin");
            e.Property(x=>x.RevisionBefore).HasColumnName("revision_before");
            e.Property(x=>x.RevisionAfter).HasColumnName("revision_after");
            e.Property(x=>x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime(6)");
            e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.TargetMembershipId})
                .HasPrincipalKey(x=>new{x.InstitutionId,x.Id}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_membership_event_target");
            e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x=>new{x.InstitutionId,x.ActorMembershipId,x.ActorUserId})
                .HasPrincipalKey(x=>new{x.InstitutionId,x.Id,x.UserId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_membership_event_actor");
        });
    }
}
