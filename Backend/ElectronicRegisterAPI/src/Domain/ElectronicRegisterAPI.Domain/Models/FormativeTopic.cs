using System.Collections.ObjectModel;
using System.Text;

namespace ElectronicRegisterAPI.Domain.Models;

public sealed record FormativeTopic(Guid Id, string Description, int DisplayOrder);
