// MT3A database-first extension. SQL-only composite integrity constraints supplement
// existing navigation FKs; they are NOT configured as duplicate EF relationships.
// Regenerate/reconcile with migrations/mt3/Scaffold-Mt3a.ps1 after SQL changes.
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    private static void ConfigureMt3aOwnership(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes().ToArray())
        {
            var table = entity.GetTableName();
            if (table == null || !Migrations.Mt3aOwnershipMigration.OwnedTables.Contains(table)) continue;
            // Shadow ownership keeps generated legacy entities untouched. EF.Property<Guid>
            // is available for future repositories; no global filter or authorization added.
            var property = entity.HasSharedClrType
                ? modelBuilder.SharedTypeEntity<Dictionary<string, object>>(entity.Name).IndexerProperty<Guid>("InstitutionId")
                : modelBuilder.Entity(entity.Name).Property<Guid>("InstitutionId");
            property
                .HasColumnName("institution_id").HasColumnType("char(36)")
                .UseCollation("ascii_general_ci").HasCharSet("ascii")
                .HasDefaultValue(Guid.Empty).ValueGeneratedOnAdd().IsConcurrencyToken();
        }
        modelBuilder.Entity<Entities.AuditEvent>().Property<Guid?>("InstitutionId")
            .HasColumnName("institution_id").HasColumnType("char(36)")
            .UseCollation("ascii_general_ci").HasCharSet("ascii").ValueGeneratedOnAdd().IsConcurrencyToken();

        ReplaceCatalogIndex<Entities.Biennium>(modelBuilder, "uq_biennium", "StartYear", "EndYear");
        ReplaceCatalogIndex<Entities.Subject>(modelBuilder, "uq_subject_name", "Name");
        ReplaceCatalogIndex<Entities.StudyArea>(modelBuilder, "uq_study_area_name", "Name");
        ReplaceCatalogIndex<Entities.StudyPath>(modelBuilder, "uq_study_path_name", "Name");
    }

    private static void ReplaceCatalogIndex<T>(ModelBuilder builder, string name, params string[] columns) where T : class
    {
        var entity = builder.Entity<T>();
        foreach (var old in entity.Metadata.GetIndexes().Where(i => i.GetDatabaseName() == name).ToArray()) entity.Metadata.RemoveIndex(old);
        entity.HasIndex(new[] { "InstitutionId" }.Concat(columns).ToArray()).IsUnique().HasDatabaseName(name);
    }
}
