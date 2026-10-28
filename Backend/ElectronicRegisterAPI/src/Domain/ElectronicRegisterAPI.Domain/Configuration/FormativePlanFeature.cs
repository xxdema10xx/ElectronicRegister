using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Configuration;

public sealed record FormativePlanFeature(Func<bool> Availability)
{
    public bool Enabled => Availability();
}
