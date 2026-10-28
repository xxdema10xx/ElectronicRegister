namespace ElectronicRegisterAPI.Infrastructure.Options;

public sealed class LegacyIdentityOptions
{
    public bool Enabled { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid[] UserIds { get; set; } = [];
    public bool AllowUnbackfilledAccounts { get; set; }
}
