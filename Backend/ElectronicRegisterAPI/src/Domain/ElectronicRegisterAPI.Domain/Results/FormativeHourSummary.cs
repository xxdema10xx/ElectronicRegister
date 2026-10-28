using System.Collections.ObjectModel;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Results;

public sealed record FormativeHourSummary(
    int TheoryPlannedMinutes,
    int InternshipPlannedMinutes,
    long TotalPlannedMinutes,
    long AssignedMinutes,
    long MissingMinutes,
    long ExcessMinutes,
    IReadOnlyList<FormativeValidationIssue> Issues
)
{
    public bool CanPublish => Issues.Count == 0;
}
