// Database First sidecar for migrations/platform-identity/001_expand.sql (reviewed scaffold
// candidate). The new User columns are deliberately not mapped: identity mutations use
// hand-written parameterized SQL and EF materialization of User must not depend on them.
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    public virtual DbSet<IdentityLifecycleEvent> IdentityLifecycleEvents { get; set; }

    private static void ConfigurePlatformIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityLifecycleEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("identity_lifecycle_event");
            entity.HasIndex(e => new { e.UserId, e.OccurredAtUtc }, "idx_lifecycle_user_time");
            entity.HasIndex(e => new { e.UserId, e.Transition, e.SecurityGeneration }, "uq_lifecycle_transition").IsUnique();
            entity.Property(e => e.Id).HasColumnName("id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.Property(e => e.OccurredAtUtc).HasMaxLength(6).HasColumnName("occurred_at_utc");
            entity.Property(e => e.RuleCode).HasMaxLength(40).HasColumnName("rule_code").UseCollation("ascii_bin").HasCharSet("ascii");
            entity.Property(e => e.RuleVersion).HasColumnName("rule_version");
            entity.Property(e => e.SecurityGeneration).HasColumnName("security_generation");
            entity.Property(e => e.Transition).HasMaxLength(16).HasColumnName("transition").UseCollation("ascii_bin").HasCharSet("ascii");
            entity.Property(e => e.UserId).HasColumnName("user_id").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("fk_lifecycle_user");
        });
    }
}
