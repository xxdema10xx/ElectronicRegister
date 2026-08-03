namespace ElectronicRegisterAPI.Infrastructure.Options;

public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string Instance { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}