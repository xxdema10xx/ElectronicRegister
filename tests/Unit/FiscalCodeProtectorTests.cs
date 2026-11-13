using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Configuration;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class FiscalCodeProtectorTests
{
    private const string Code = "RSSMRA80A01H501U";

    private static PlatformIdentitySettings Settings(byte[]? hmac = null, byte[]? encryption = null, string encryptionKeyId = "enc-1", bool enabled = true) =>
        new(enabled, "its.example", "hmac-1", hmac ?? Enumerable.Repeat((byte)7, 32).ToArray(), encryptionKeyId, encryption ?? Enumerable.Repeat((byte)9, 32).ToArray(), 36, TimeSpan.FromDays(1), 200);

    [Fact]
    public void HmacIsDeterministicKeyedAndUppercaseHex()
    {
        var a = new FiscalCodeProtector(Settings());
        Assert.Equal(a.Hmac(Code), new FiscalCodeProtector(Settings()).Hmac(Code));
        Assert.Matches("^[0-9A-F]{64}$", a.Hmac(Code));
        Assert.NotEqual(a.Hmac(Code), a.Hmac("BNCLRA85M41F205Z"));
        Assert.NotEqual(a.Hmac(Code), new FiscalCodeProtector(Settings(hmac: Enumerable.Repeat((byte)8, 32).ToArray())).Hmac(Code));
        Assert.Equal(Convert.ToHexString(HMACSHA256.HashData(Enumerable.Repeat((byte)7, 32).ToArray(), "RSSMRA80A01H501U"u8.ToArray())), a.Hmac(Code));
    }

    [Fact]
    public void EncryptionRoundTripsWithAFreshNonceEveryTime()
    {
        var protector = new FiscalCodeProtector(Settings());
        var first = protector.Protect(Code);
        var second = protector.Protect(Code);
        Assert.Equal(first.Hmac, second.Hmac);
        Assert.Equal("hmac-1", first.HmacKeyId);
        Assert.Equal("enc-1", first.CipherKeyId);
        Assert.Equal(12 + 16 + 16, first.Cipher.Length);
        Assert.False(first.Cipher.AsSpan(0, 12).SequenceEqual(second.Cipher.AsSpan(0, 12)));
        Assert.False(first.Cipher.SequenceEqual(second.Cipher));
        Assert.Equal(Code, protector.Unprotect(first));
        Assert.Equal(Code, protector.Unprotect(second));
        Assert.DoesNotContain("RSSMRA", System.Text.Encoding.ASCII.GetString(first.Cipher));
    }

    [Fact]
    public void WrongKeyTamperingOrMovedCiphertextAreRejected()
    {
        var protector = new FiscalCodeProtector(Settings());
        var stored = protector.Protect(Code);
        Assert.ThrowsAny<CryptographicException>(() => new FiscalCodeProtector(Settings(encryption: Enumerable.Repeat((byte)1, 32).ToArray())).Unprotect(stored));
        Assert.ThrowsAny<CryptographicException>(() => new FiscalCodeProtector(Settings(encryptionKeyId: "enc-2")).Unprotect(stored));
        var tampered = stored.Cipher.ToArray();
        tampered[20] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(stored with { Cipher = tampered }));
        // A ciphertext copied onto another person's HMAC does not authenticate.
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(stored with { Hmac = protector.Hmac("BNCLRA85M41F205Z") }));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(stored with { Cipher = stored.Cipher[..20] }));
    }

    [Fact]
    public void StagingTokensRoundTripAndAreBoundToTheirPurpose()
    {
        var protector = new FiscalCodeProtector(Settings());
        var token = protector.Seal(Code);
        Assert.StartsWith("cf1:enc-1:", token);
        Assert.DoesNotContain(Code, token);
        Assert.True(protector.IsSealed(token));
        Assert.False(protector.IsSealed(Code));
        Assert.Equal(Code, protector.Unseal(token));
        Assert.NotEqual(token, protector.Seal(Code));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unseal(Code));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unseal("cf1:enc-1:not-base64!"));
        Assert.ThrowsAny<CryptographicException>(() => new FiscalCodeProtector(Settings(encryption: Enumerable.Repeat((byte)1, 32).ToArray())).Unseal(token));
        Assert.Throws<ArgumentException>(() => protector.Seal(new string('A', 65)));
    }

    [Fact]
    public void DisabledPlatformIdentityRefusesEveryOperationAndHidesKeys()
    {
        var protector = new FiscalCodeProtector(PlatformIdentitySettings.Disabled);
        Assert.Throws<UnauthorizedAccessException>(() => protector.Hmac(Code));
        Assert.Throws<UnauthorizedAccessException>(() => protector.Protect(Code));
        Assert.Throws<UnauthorizedAccessException>(() => protector.Seal(Code));
        Assert.DoesNotContain("7", Settings().ToString().Replace("its.example", ""));
    }
}
