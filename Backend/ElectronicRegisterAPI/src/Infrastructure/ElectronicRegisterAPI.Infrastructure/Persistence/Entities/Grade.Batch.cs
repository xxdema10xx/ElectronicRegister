namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

// Grade columns added by migrations/grade-scale (20261117): kept outside the scaffolded Grade.cs.
public partial class Grade
{
    public string? Description { get; set; }

    public Guid? BatchEventId { get; set; }
}
