using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IFormativeDraftBuilder
{
    FormativePlanSnapshot Build(FormativePlanSnapshot prior, ReplaceFormativeDraft request);
}
