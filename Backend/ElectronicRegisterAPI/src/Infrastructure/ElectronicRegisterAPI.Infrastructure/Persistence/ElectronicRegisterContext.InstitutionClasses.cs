using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    private static void ConfigureInstitutionClasses(ModelBuilder b)
    {
        var e = b.Entity<InstitutionClassEvent>();
        e.ToTable("institution_class_event");
        e.HasKey(x => new { x.InstitutionId, x.Id });
        e.Property(x => x.InstitutionId).HasColumnName("institution_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x => x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x => x.ActorMembershipId).HasColumnName("actor_membership_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x => x.ClassId).HasColumnName("class_id").HasColumnType("char(36)").HasCharSet("ascii").UseCollation("ascii_general_ci");
        e.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint(20)");
        e.Property(x => x.Action).HasColumnName("action").HasMaxLength(32).HasCharSet("ascii").UseCollation("ascii_bin");
        e.Property(x => x.BeforeJson).HasColumnName("before_json").HasColumnType("longtext");
        e.Property(x => x.AfterJson).HasColumnName("after_json").HasColumnType("longtext");
        e.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("datetime(6)");
        e.HasIndex(x => new { x.InstitutionId, x.ClassId, x.Revision }).IsUnique().HasDatabaseName("uq_class_event_revision");
        e.HasIndex(x => new { x.InstitutionId, x.ActorMembershipId, x.ActorUserId }).HasDatabaseName("idx_class_event_actor");
        e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_class_event_owner");
        e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(x => new { x.InstitutionId, x.ActorMembershipId, x.ActorUserId })
            .HasPrincipalKey(x => new { x.InstitutionId, x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_class_event_actor");
    }
}
