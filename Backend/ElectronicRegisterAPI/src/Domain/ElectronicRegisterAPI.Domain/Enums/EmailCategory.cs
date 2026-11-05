using ElectronicRegisterAPI.Domain.Serialization;
using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

// These are generation categories, not IAM roles. A request selects exactly one.
[JsonConverter(typeof(EmailCatalogConverter<EmailCategory>))]
public enum EmailCategory
{
    Student,
    Teacher,
    Staff,
    InstitutionAdmin
}
