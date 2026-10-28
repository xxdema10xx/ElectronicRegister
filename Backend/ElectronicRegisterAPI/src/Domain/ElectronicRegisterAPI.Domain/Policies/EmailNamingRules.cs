using ElectronicRegisterAPI.Domain.Enums;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmailNamingRules(
    EmailNameCase Case = EmailNameCase.Lower,
    EmailAccentHandling Accents = EmailAccentHandling.LatinFoldV1,
    string WordSeparator = "",
    EmailPunctuationHandling Punctuation = EmailPunctuationHandling.Remove
);
