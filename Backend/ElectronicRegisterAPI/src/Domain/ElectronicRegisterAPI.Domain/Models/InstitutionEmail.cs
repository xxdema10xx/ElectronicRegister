using System.Text.Json.Serialization;
namespace ElectronicRegisterAPI.Domain.Models;

// These are generation categories, not IAM roles. A request selects exactly one.
[JsonConverter(typeof(EmailCatalogConverter<EmailCategory>))]
public enum EmailCategory { Student, Teacher, Staff, InstitutionAdmin }
[JsonConverter(typeof(EmailCatalogConverter<EmailNameCase>))]
public enum EmailNameCase { Lower, Upper }
[JsonConverter(typeof(EmailCatalogConverter<EmailAccentHandling>))]
public enum EmailAccentHandling { RejectNonAscii, LatinFoldV1 }
[JsonConverter(typeof(EmailCatalogConverter<EmailPunctuationHandling>))]
public enum EmailPunctuationHandling { Remove, WordBoundary }
[JsonConverter(typeof(EmailCatalogConverter<EmailCollisionStrategy>))]
public enum EmailCollisionStrategy { Reject, NumericSuffix }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailNamingRules(EmailNameCase Case = EmailNameCase.Lower,
    EmailAccentHandling Accents = EmailAccentHandling.LatinFoldV1,
    string WordSeparator = "", EmailPunctuationHandling Punctuation = EmailPunctuationHandling.Remove);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailTemplate(string LocalPartTemplate, string Domain, EmailNamingRules Names,
    EmailCollisionStrategy Collision = EmailCollisionStrategy.Reject, string NumericSuffix = "{n}",
    int SuffixStart = 2, int MaximumAttempts = 32);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailGenerationInput(string? FirstName, string? LastName);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GeneratedInstitutionEmail(string Email, string NormalizedEmail,
    string LocalPart, string Domain, string[] Warnings);

public interface IInstitutionEmailGenerator
{
    void Validate(EmailTemplate template);
    GeneratedInstitutionEmail Preview(EmailTemplate template, EmailGenerationInput input);
    GeneratedInstitutionEmail Candidate(EmailTemplate template, EmailGenerationInput input, int attempt);
    string ValidateAddress(string address, string requiredDomain);
}
