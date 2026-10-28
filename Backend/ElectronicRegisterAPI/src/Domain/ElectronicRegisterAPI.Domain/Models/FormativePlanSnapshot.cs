using ElectronicRegisterAPI.Domain.Enums;
using System.Collections.ObjectModel;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record FormativePlanSnapshot(
    Guid Id,
    Guid InstitutionId,
    Guid StudyPathId,
    Guid PlanId,
    int VersionNumber,
    string Name,
    FormativeVersionStatus Status,
    long Revision,
    int TheoryPlannedMinutes,
    int InternshipPlannedMinutes,
    IReadOnlyList<FormativeUnitSnapshot> Units
)
{
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
}
