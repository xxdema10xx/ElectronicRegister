using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using ElectronicRegisterAPI.Domain.Policies;

namespace ElectronicRegisterAPI.Tests.Integration;

// Synthetic platform identity configuration for tests: random keys generated once per test
// process (so every host of the process shares HMAC and ciphertexts), never persisted or
// committed, and an example.test domain covered by the D-B fixture exception.
internal static class PlatformIdentityTestConfig
{
    internal const string Domain = "platform.example.test";
    private static readonly string HmacKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    internal static IEnumerable<KeyValuePair<string, string?>> Values(bool enabled = true, string interval = "1.00:00:00", int months = 36, int batch = 200) =>
    [
        new("Multitenancy:PlatformIdentity:Enabled", enabled ? "true" : "false"),
        new("Multitenancy:PlatformIdentity:EmailDomain", Domain),
        new("Multitenancy:PlatformIdentity:FiscalCodeHmacKeyId", "test-hmac-1"),
        new("Multitenancy:PlatformIdentity:FiscalCodeHmacKey", HmacKey),
        new("Multitenancy:PlatformIdentity:FiscalCodeEncryptionKeyId", "test-enc-1"),
        new("Multitenancy:PlatformIdentity:FiscalCodeEncryptionKey", EncryptionKey),
        new("Multitenancy:PlatformIdentity:StudentClosureAfterMonths", months.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("Multitenancy:PlatformIdentity:LifecycleInterval", interval),
        new("Multitenancy:PlatformIdentity:LifecycleBatchSize", batch.ToString(System.Globalization.CultureInfo.InvariantCulture))
    ];

    internal static string[] Arguments(bool enabled = true, string interval = "1.00:00:00", int months = 36, int batch = 200) =>
        Values(enabled, interval, months, batch).Select(v => "--" + v.Key + "=" + v.Value).ToArray();

    // A random, structurally valid personal fiscal code (with the official check character).
    internal static string NewFiscalCode()
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string months = "ABCDEHLMPRST";
        var body = new string(RandomNumberGenerator.GetItems<char>(letters, 6))
            + RandomNumberGenerator.GetInt32(0, 100).ToString("00")
            + months[RandomNumberGenerator.GetInt32(months.Length)]
            + (RandomNumberGenerator.GetInt32(1, 29) + (RandomNumberGenerator.GetInt32(2) == 0 ? 0 : 40)).ToString("00")
            + letters[RandomNumberGenerator.GetInt32(letters.Length)]
            + RandomNumberGenerator.GetInt32(0, 1000).ToString("000");
        return body + FiscalCodeRules.Check(body);
    }

    internal static string Address(string firstName, string lastName, int number = 1) =>
        PlatformEmailRules.Candidate(PlatformEmailRules.Base(firstName, lastName)!, number, Domain);
}
