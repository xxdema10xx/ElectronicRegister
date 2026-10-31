using ElectronicRegisterAPI.Domain.DTOs.Email;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IInstitutionEmailGenerator
{
    void Validate(EmailTemplate template);
    GeneratedInstitutionEmail Preview(EmailTemplate template, EmailGenerationInput input);
    GeneratedInstitutionEmail Candidate(EmailTemplate template, EmailGenerationInput input, int attempt);
    string ValidateAddress(string address, string requiredDomain);
}
