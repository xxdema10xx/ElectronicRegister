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
        int lifecycleBatchSize,
        int importHashParallelism = 0
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
        ImportHashParallelism = importHashParallelism;
    }

    public static PlatformIdentitySettings Disabled { get; } = new(false, "", "", [], "", [], 0, TimeSpan.Zero, 0);

    public bool Enabled { get; }
    public string EmailDomain { get; }
    public string HmacKeyId { get; }
    public string EncryptionKeyId { get; }
    public int StudentClosureAfterMonths { get; }
    public TimeSpan LifecycleInterval { get; }
    public int LifecycleBatchSize { get; }

    // Threads that compute BCrypt hashes for imports, shared by every concurrent import of the
    // process; 0 = one per available core. Never above the available cores nor MaximumImportHashParallelism.
    public int ImportHashParallelism { get; }
    public const int MaximumImportHashParallelism = 32;

    public ReadOnlySpan<byte> HmacKeyBytes => _hmacKey;
    public ReadOnlySpan<byte> EncryptionKeyBytes => _encryptionKey;

    public void RequireEnabled()
    {
        if (!Enabled)
            throw new UnauthorizedAccessException("Identità di piattaforma non abilitata.");
    }

    public override string ToString() => $"PlatformIdentity(Enabled={Enabled}, Domain={EmailDomain})";
}
