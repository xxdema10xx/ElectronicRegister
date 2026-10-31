namespace ElectronicRegisterAPI.Infrastructure.Options;

public sealed class MicrosoftIdentityBinding
{
    public Guid TenantId { get; set; }
    public Guid ObjectId { get; set; }
    public Guid UserId { get; set; }
}
