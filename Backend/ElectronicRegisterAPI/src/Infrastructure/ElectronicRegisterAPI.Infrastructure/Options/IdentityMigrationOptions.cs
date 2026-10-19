namespace ElectronicRegisterAPI.Infrastructure.Options;

public sealed class LegacyIdentityOptions
{
    public bool Enabled { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid[] UserIds { get; set; } = [];
    public bool AllowUnbackfilledAccounts { get; set; }
}

public sealed class MicrosoftIdentityBinding
{
    public Guid TenantId { get; set; }
    public Guid ObjectId { get; set; }
    public Guid UserId { get; set; }
}

public sealed class MicrosoftIdentityOptions
{
    public MicrosoftIdentityBinding[] Bindings { get; set; } = [];
}
