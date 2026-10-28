using System.Text.Json.Serialization;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Configuration;

public sealed record InstitutionImportFeature(
    bool Enabled,
    bool SchoolEnabled,
    ImportParserLimits Limits,
    int TtlMinutes = 30,
    int MaxStageBytes = 2 * 1024 * 1024,
    int MaxPendingPreviews = 20
)
{
    [JsonIgnore]
    public Func<bool>? CurrentAvailability { get; init; }
    public bool IsEnabled => CurrentAvailability?.Invoke() ?? (Enabled && SchoolEnabled);

    public void Validate()
    {
        Limits.Validate();
        if (TtlMinutes is < 1 or > 120 || MaxStageBytes is < 1024 or > 3 * 1024 * 1024 || MaxPendingPreviews is < 1 or > 100)
            throw new ArgumentException("Configurazione staging non valida.");
    }
}
