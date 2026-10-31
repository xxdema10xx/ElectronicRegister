using System.Collections.ObjectModel;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record FormativeModuleSnapshot(
    Guid Id,
    string? Code,
    string Name,
    string? Description,
    int PlannedMinutes,
    int DisplayOrder,
    Guid? SubjectId,
    IReadOnlyList<FormativeTopic> Competencies,
    IReadOnlyList<FormativeTopic> Contents
);
