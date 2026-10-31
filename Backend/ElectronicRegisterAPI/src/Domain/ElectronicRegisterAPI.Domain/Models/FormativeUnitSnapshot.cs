using System.Collections.ObjectModel;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record FormativeUnitSnapshot(
    Guid Id,
    string? Code,
    string Name,
    string? Description,
    int DisplayOrder,
    IReadOnlyList<FormativeModuleSnapshot> Modules
)
{
    public long PlannedMinutes => Modules.Sum(m => (long)m.PlannedMinutes);
}
