// MT-1 sidecar: derived from migrations/mt1/001_expand.sql by database-first scaffolding.
// Kept outside generated ElectronicRegisterContext.cs. Do not scaffold over this file.
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    public virtual DbSet<Institution> Institutions { get; set; }
    public virtual DbSet<Campus> Campuses { get; set; }
    public virtual DbSet<InstitutionMembership> InstitutionMemberships { get; set; }
    public virtual DbSet<InstitutionMembershipRole> InstitutionMembershipRoles { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        ConfigureMt3Profiles(modelBuilder);
        ConfigureMt3aOwnership(modelBuilder);
        modelBuilder.Entity<Campus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("campus")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.InstitutionId, "idx_campus_institution");

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.Address)
                .HasMaxLength(300)
                .HasColumnName("address");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.InstitutionId)
                .HasColumnName("institution_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Province)
                .HasMaxLength(100)
                .HasColumnName("province");

            entity.HasOne(d => d.Institution).WithMany(p => p.Campuses)
                .HasForeignKey(d => d.InstitutionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_campus_institution");
        });

        modelBuilder.Entity<Institution>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("institution")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Code, "uq_institution_code").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.Code)
                .HasMaxLength(64)
                .HasColumnName("code")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.CreatedAt)
                .HasMaxLength(6)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("created_at");
            entity.Property(e => e.LegalTaxCode)
                .HasMaxLength(32)
                .HasColumnName("legal_tax_code");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Region)
                .HasMaxLength(100)
                .HasColumnName("region");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'provisioning'")
                .HasColumnType("enum('provisioning','active','suspended','archived')")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasMaxLength(6)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<InstitutionMembership>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity
                .ToTable("institution_membership")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.InstitutionId, "idx_membership_institution");

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

            entity.HasOne(d => d.Institution).WithMany(p => p.InstitutionMemberships)
                .HasForeignKey(d => d.InstitutionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_membership_institution");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_membership_user");
        });

        modelBuilder.Entity<InstitutionMembershipRole>(entity =>
        {
            entity.HasKey(e => new { e.MembershipId, e.Role })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("institution_membership_role")
                .UseCollation("utf8mb4_unicode_ci");

            entity.Property(e => e.MembershipId)
                .HasColumnName("membership_id")
                .UseCollation("ascii_general_ci")
                .HasCharSet("ascii");
            entity.Property(e => e.Role)
                .HasColumnType("enum('InstitutionAdmin','Teacher','Student')")
                .HasColumnName("role");

            entity.HasOne(d => d.Membership).WithMany(p => p.InstitutionMembershipRoles)
                .HasForeignKey(d => d.MembershipId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_membership_role_membership");
        });
        ConfigureMt4(modelBuilder);
        ConfigureMt4c(modelBuilder);
        ConfigureInstitutionClasses(modelBuilder);
        ConfigureStudentTransfers(modelBuilder);
        ConfigureInstitutionAdministration(modelBuilder);
        ConfigureMt5bSecurity(modelBuilder);
        ConfigureMt5d(modelBuilder);

    }
}
