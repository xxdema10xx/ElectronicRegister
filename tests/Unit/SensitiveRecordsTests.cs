using System;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Persistence.Import;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

// A logged or thrown instance must never reveal a fiscal code, its HMAC or ciphertext, a hash,
// a temporary password or a generated address.
public sealed class SensitiveRecordsTests
{
    private const string FiscalCode = "RSSMRA85T10A562S";
    private const string Hmac = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";

    [Fact]
    public void ToStringOmitsEverySecret()
    {
        var protectedCode = new ProtectedFiscalCode(Hmac, "h1", [1, 2, 3, 4, 5], "e1");
        var texts = new[]
        {
            protectedCode.ToString(),
            new CreateInstitutionAccount(Guid.NewGuid(), "Mario", "Rossi", FiscalCode, [InstitutionRole.Student], null, null, null, "motivo").ToString(),
            new ImportAccountRequest(7, "Mario", "Rossi", "mario.rossi", null, protectedCode, "$2a$11$hashhashhashhashhash", "Temp-Pa55word!").ToString(),
            new ImportAccountRow(7, Guid.NewGuid(), Guid.NewGuid(), "mario.rossi@its.example", "Temp-Pa55word!").ToString()
        };
        foreach (var text in texts)
            foreach (var secret in new[] { FiscalCode, Hmac, "hashhash", "Temp-Pa55word!", "mario.rossi", "Mario", "Rossi", "1, 2, 3" })
                Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
    }
}
