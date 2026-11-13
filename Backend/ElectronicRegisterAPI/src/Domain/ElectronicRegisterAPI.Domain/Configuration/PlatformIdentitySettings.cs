namespace ElectronicRegisterAPI.Domain.Configuration;

// Validated owner configuration (Multitenancy:PlatformIdentity). Keys never have defaults
// and are never rendered: ToString deliberately omits every secret.
public sealed class PlatformIdentitySettings
{
    private readonly byte[] _hmacKey;
    private readonly byte[] _encryptionKey;

    public PlatformIdentitySettings(
        bool enabled,
        string emailDomain,
        string hmacKeyId,
        byte[] hmacKey,
        string encryptionKeyId,
        byte[] encryptionKey,
        int studentClosureAfterMonths,
        TimeSpan lifecycleInterval,
        int lifecycleBatchSize
    )
    {
        Enabled = enabled;
        EmailDomain = emailDomain;
        HmacKeyId = hmacKeyId;
        _hmacKey = hmacKey.ToArray();
        EncryptionKeyId = encryptionKeyId;
        _encryptionKey = encryptionKey.ToArray();
        StudentClosureAfterMonths = studentClosureAfterMonths;
        LifecycleInterval = lifecycleInterval;
        LifecycleBatchSize = lifecycleBatchSize;
    }

    public static PlatformIdentitySettings Disabled { get; } = new(false, "", "", [], "", [], 0, TimeSpan.Zero, 0);

    public bool Enabled { get; }
    public string EmailDomain { get; }
    public string HmacKeyId { get; }
    public string EncryptionKeyId { get; }
    public int StudentClosureAfterMonths { get; }
    public TimeSpan LifecycleInterval { get; }
    public int LifecycleBatchSize { get; }

    public ReadOnlySpan<byte> HmacKeyBytes => _hmacKey;
    public ReadOnlySpan<byte> EncryptionKeyBytes => _encryptionKey;

    public void RequireEnabled()
    {
        if (!Enabled)
            throw new UnauthorizedAccessException("Identità di piattaforma non abilitata.");
    }

    public override string ToString() => $"PlatformIdentity(Enabled={Enabled}, Domain={EmailDomain})";
}
