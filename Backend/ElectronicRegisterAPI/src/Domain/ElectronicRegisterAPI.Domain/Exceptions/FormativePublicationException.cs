using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Exceptions;

public sealed class FormativePublicationException(FormativeHourSummary summary) : BusinessRuleException("Piano formativo incompleto o monte ore incoerente.")
{
    public FormativeHourSummary Summary { get; } = summary;
}
