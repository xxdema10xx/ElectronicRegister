using System.Text.RegularExpressions;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.Policies;
using Microsoft.Extensions.Configuration;

namespace ElectronicRegisterAPI.Infrastructure.Options;

// Multitenancy:PlatformIdentity. Rollout flag false by default; no key, domain or duration
// has a default in code. With the flag enabled, startup is refused unless every value is
// present and valid. Error messages never contain configured values.
public sealed class PlatformIdentityOptions
{
    public const string Section = "Multitenancy:PlatformIdentity";
    private static readonly Regex KeyId = new("^[A-Za-z0-9._-]{1,32}$", RegexOptions.CultureInvariant);

    public bool Enabled { get; set; }
    public string? EmailDomain { get; set; }
    public string? FiscalCodeHmacKeyId { get; set; }
    public string? FiscalCodeHmacKey { get; set; }
    public string? FiscalCodeEncryptionKeyId { get; set; }
    public string? FiscalCodeEncryptionKey { get; set; }
    public int? StudentClosureAfterMonths { get; set; }
    public TimeSpan? LifecycleInterval { get; set; }
    public int? LifecycleBatchSize { get; set; }

    public static PlatformIdentitySettings Read(IConfiguration configuration)
    {
        var options = new PlatformIdentityOptions();
        configuration.GetSection(Section).Bind(options);
        return options.ToSettings();
    }

    public PlatformIdentitySettings ToSettings()
    {
        if (!Enabled)
            return PlatformIdentitySettings.Disabled;
        Require(PlatformEmailRules.IsValidDomain(EmailDomain), "EmailDomain");
        Require(FiscalCodeHmacKeyId is not null && KeyId.IsMatch(FiscalCodeHmacKeyId), "FiscalCodeHmacKeyId");
        Require(FiscalCodeEncryptionKeyId is not null && KeyId.IsMatch(FiscalCodeEncryptionKeyId), "FiscalCodeEncryptionKeyId");
        var hmac = Key(FiscalCodeHmacKey, "FiscalCodeHmacKey");
        var encryption = Key(FiscalCodeEncryptionKey, "FiscalCodeEncryptionKey");
        Require(hmac.Length >= 32 && hmac.Length <= 128, "FiscalCodeHmacKey");
        Require(encryption.Length == 32, "FiscalCodeEncryptionKey");
        Require(!hmac.AsSpan().SequenceEqual(encryption), "FiscalCodeEncryptionKey");
        Require(StudentClosureAfterMonths is >= 1 and <= 240, "StudentClosureAfterMonths");
        Require(LifecycleInterval is { } interval && interval >= TimeSpan.FromMinutes(1) && interval <= TimeSpan.FromDays(7), "LifecycleInterval");
        Require(LifecycleBatchSize is >= 1 and <= 1000, "LifecycleBatchSize");
        return new(true, EmailDomain!, FiscalCodeHmacKeyId!, hmac, FiscalCodeEncryptionKeyId!, encryption, StudentClosureAfterMonths!.Value, LifecycleInterval!.Value, LifecycleBatchSize!.Value);
    }

    private static byte[] Key(string? value, string name)
    {
        Require(!string.IsNullOrWhiteSpace(value), name);
        try
        {
            return Convert.FromBase64String(value!);
        }
        catch (FormatException)
        {
            throw Invalid(name);
        }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
            throw Invalid(name);
    }

    private static InvalidOperationException Invalid(string name) => new($"Platform identity configuration is incomplete or invalid: {Section}:{name}.");
}
