using System.Collections.ObjectModel;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Results;

public sealed record FormativeValidationIssue(string Code, Guid ResourceId);
