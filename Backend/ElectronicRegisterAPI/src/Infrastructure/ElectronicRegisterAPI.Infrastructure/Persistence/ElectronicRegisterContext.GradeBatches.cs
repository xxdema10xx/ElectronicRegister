// Grade scale 20261117 (migrations/grade-scale): optional description and the batch event
// that created a manual grade. The unique key makes a replayed or concurrent batch with the
// same event collide instead of duplicating grades.
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    private static void ConfigureGradeBatches(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.Grade>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(200).HasColumnName("description");
            entity.Property(e => e.BatchEventId).HasColumnName("batch_event_id").HasColumnType("char(36)").UseCollation("ascii_general_ci").HasCharSet("ascii");
            entity.HasIndex(new[] { "InstitutionId", nameof(Entities.Grade.BatchEventId), nameof(Entities.Grade.StudentId) }).IsUnique().HasDatabaseName("uq_grade_batch_student");
        });
    }
}
