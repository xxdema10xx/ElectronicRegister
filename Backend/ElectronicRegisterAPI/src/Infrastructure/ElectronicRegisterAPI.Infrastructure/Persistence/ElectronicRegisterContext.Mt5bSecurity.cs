using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    public virtual DbSet<InstitutionMembershipSecurityState> InstitutionMembershipSecurityStates { get; set; }

    // Database-first MT5B sidecar. FK retained explicitly: isolated-table scaffold omits
    // the unselected principal. No navigation or global identity graph is introduced.
    private static void ConfigureMt5bSecurity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstitutionMembershipSecurityState>(e =>
        {
            e.ToTable("institution_membership_security_state").UseCollation("utf8mb4_unicode_ci");
            e.HasKey(s => new { s.InstitutionId, s.MembershipId });
            e.HasIndex(s => s.MembershipId, "uq_membership_security_identity").IsUnique();
            e.Property(s => s.InstitutionId).HasColumnName("institution_id").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(s => s.MembershipId).HasColumnName("membership_id").HasCharSet("ascii").UseCollation("ascii_general_ci");
            e.Property(s => s.SecurityGeneration).HasColumnName("security_generation").HasDefaultValue(0L).ValueGeneratedOnAddOrUpdate();
            e.HasOne<InstitutionMembership>().WithMany().HasForeignKey(s => new { s.InstitutionId, s.MembershipId })
                .HasPrincipalKey(m => new { m.InstitutionId, m.Id }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_membership_security_owner");
        });
    }
}
