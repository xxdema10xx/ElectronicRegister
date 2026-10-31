using System.Collections.ObjectModel;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Enums;

// Pure domain values. These types are deliberately separate from Database First entities.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<FormativeVersionStatus>))]
public enum FormativeVersionStatus
{
    Draft,
    Published
}
