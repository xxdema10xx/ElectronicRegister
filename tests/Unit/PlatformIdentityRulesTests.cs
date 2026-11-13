using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Policies;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class PlatformIdentityRulesTests
{
    private const string Domain = "its.example";

    [Theory]
    [InlineData("Mario", "Rossi", "mario.rossi")]
    [InlineData("  Niccolò ", "Àlbèrti", "niccolo.alberti")]
    [InlineData("Anna Maria", "De Luca", "anna.maria.de.luca")]
    [InlineData("Gian-Luca", "D'Angelo", "gian.luca.dangelo")]
    [InlineData("Jean’Pierre", "O’Neill", "jeanpierre.oneill")]
    [InlineData("Søren", "Łukasz-Æsir", "soren.lukasz.aesir")]
    [InlineData("Straße", "Ðorđe", "strasse.dorde")]
    [InlineData("Maria  --  Grazia", "Di_Stefano.", "maria.grazia.di.stefano")]
    [InlineData("ÉLODIE", "MÜLLER", "elodie.muller")]
    [InlineData("Zoë", "Brontë 3", "zoe.bronte.3")]
    public void NamesNormalizeDeterministically(string first, string last, string expected) =>
        Assert.Equal(expected, PlatformEmailRules.Base(first, last));

    [Theory]
    [InlineData("李", "Rossi")]
    [InlineData("Mario", "'''")]
    [InlineData("", "Rossi")]
    [InlineData("Mario", null)]
    public void UnrepresentableNamesAreRejectedInsteadOfInventingAnIdentifier(string? first, string? last) =>
        Assert.Null(PlatformEmailRules.Base(first, last));

    [Fact]
    public void CandidatesAppendIncreasingSuffixesAndRespectLengthLimits()
    {
        Assert.Equal("mario.rossi@its.example", PlatformEmailRules.Candidate("mario.rossi", 1, Domain));
        Assert.Equal("mario.rossi2@its.example", PlatformEmailRules.Candidate("mario.rossi", 2, Domain));
        Assert.Equal("mario.rossi10@its.example", PlatformEmailRules.Candidate("mario.rossi", 10, Domain));
        var longBase = new string('a', 40) + "." + new string('b', 40);
        foreach (var n in new[] { 1, 2, 99, 9999 })
        {
            var address = PlatformEmailRules.Candidate(longBase, n, Domain);
            var local = address.Split('@')[0];
            Assert.True(local.Length <= PlatformEmailRules.MaxLocalLength, address);
            var head = n == 1 ? local : local[..^n.ToString().Length];
            Assert.False(head.EndsWith('.'), address);
            Assert.StartsWith(PlatformEmailRules.LookupPrefix(longBase), address);
        }
        Assert.Throws<ArgumentException>(() => PlatformEmailRules.Candidate("mario.rossi", 0, Domain));
        Assert.Throws<ArgumentException>(() => PlatformEmailRules.Candidate("mario.rossi", 10000, Domain));
        Assert.Throws<ArgumentException>(() => PlatformEmailRules.Candidate("mario.rossi", 1, "Its.Example"));
        var hugeDomain = string.Join('.', Enumerable.Repeat(new string('d', 60), 4)) + ".it";
        Assert.False(PlatformEmailRules.IsValidDomain(hugeDomain));
    }

    [Theory]
    [InlineData("its.it", true)]
    [InlineData("scuola.its-umbria.it", true)]
    [InlineData("localhost", false)]
    [InlineData("ITS.it", false)]
    [InlineData("-its.it", false)]
    [InlineData("its..it", false)]
    [InlineData("its.it.", false)]
    public void PlatformDomainIsValidated(string domain, bool valid) => Assert.Equal(valid, PlatformEmailRules.IsValidDomain(domain));

    [Fact]
    public void PlanNeverReissuesAnAddressAndSuffixesHomonymsInCallOrder()
    {
        var plan = new PlatformEmailPlan(Domain, ["MARIO.ROSSI@its.example", "mario.rossi3@its.example", "mario.rossini@its.example"]);
        Assert.Equal("mario.rossi2@its.example", plan.Take("mario.rossi"));
        Assert.Equal("mario.rossi4@its.example", plan.Take("mario.rossi"));
        plan.MarkTaken("mario.rossi5@its.example");
        Assert.Equal("mario.rossi6@its.example", plan.Take("mario.rossi"));
        Assert.Equal("anna.bianchi@its.example", plan.Take("anna.bianchi"));
        Assert.Equal("anna.bianchi2@its.example", plan.Take("anna.bianchi"));
    }

    [Fact]
    public void PlanFailsClosedWhenEveryCandidateIsTaken()
    {
        var taken = Enumerable.Range(1, PlatformEmailRules.MaxCandidates).Select(n => PlatformEmailRules.Candidate("a.b", n, Domain));
        Assert.Throws<AccountEmailUnavailableException>(() => new PlatformEmailPlan(Domain, taken).Take("a.b"));
    }

    [Theory]
    [InlineData("RSSMRA80A01H501U", true)]
    [InlineData(" rssmra80a01h501u ", true)]
    [InlineData("RSSMRA80A01H501X", false)]
    [InlineData("RSSMRA80Z01H501U", false)]
    [InlineData("RSSMRA80A32H501U", false)]
    [InlineData("RSSMRA80A01H501", false)]
    [InlineData("12345678901", false)]
    [InlineData("RSSMRA80A01H5 1U", false)]
    [InlineData("RSSMRA8OA01H501U", false)]
    [InlineData("", false)]
    public void FiscalCodesAreNormalizedAndCheckCharacterValidated(string value, bool valid) =>
        Assert.Equal(valid, FiscalCodeRules.IsValid(FiscalCodeRules.Normalize(value)));

    [Fact]
    public void OmocodiaSubstitutionsAndFemaleBirthDaysAreAccepted()
    {
        // Positions 15, 14 and 11 (1-based) replaced by their omocodia letters (1->M, 0->L, 1->M).
        foreach (var body in new[] { "RSSMRA80A01H50M", "RSSMRA80A01H5LM", "RSSMRA80A0MH5LM", "BNCLRA85M41F205" })
            Assert.True(FiscalCodeRules.IsValid(body + FiscalCodeRules.Check(body)), body);
        Assert.False(FiscalCodeRules.IsValid("RSSMRA80A01H50A" + FiscalCodeRules.Check("RSSMRA80A01H50A")));
        Assert.False(FiscalCodeRules.IsValid("BNCLRA85M81F205" + FiscalCodeRules.Check("BNCLRA85M81F205")));
    }

    [Theory]
    [InlineData("Niccolò", "niccolo", true)]
    [InlineData(" DE LUCA ", "de luca", true)]
    [InlineData("Rossi", "Rosso", false)]
    [InlineData("Anna Maria", "AnnaMaria", false)]
    public void LinkedProfileNamesCompareWithoutCaseAndAccents(string left, string right, bool same) =>
        Assert.Equal(same, PersonNameComparison.SameName(left, right));

    private static CreateInstitutionAccount Request(
        InstitutionRole[]? roles = null,
        string first = "Mario",
        string last = "Rossi",
        string fiscalCode = "RSSMRA80A01H501U",
        Guid? studentId = null,
        Guid? teacherId = null,
        Guid? classId = null
    ) => new(Guid.NewGuid(), first, last, fiscalCode, roles ?? [InstitutionRole.Student], studentId, teacherId, classId, "Iscrizione");

    [Fact]
    public void AccountRequestsRequireNamesAndFiscalCodeForEveryRole()
    {
        foreach (var roles in new[] { new[] { InstitutionRole.Student }, [InstitutionRole.Teacher], [InstitutionRole.Staff], [InstitutionRole.InstitutionAdmin, InstitutionRole.Staff] })
        {
            InstitutionAccountRules.Validate(Request(roles));
            Assert.Contains("Nome", Assert.Throws<ArgumentException>(() => InstitutionAccountRules.Validate(Request(roles, first: " "))).Message);
            Assert.Contains("Cognome", Assert.Throws<ArgumentException>(() => InstitutionAccountRules.Validate(Request(roles, last: ""))).Message);
            Assert.Contains("fiscale", Assert.Throws<ArgumentException>(() => InstitutionAccountRules.Validate(Request(roles, fiscalCode: "RSSMRA80A01H501X"))).Message);
        }
        // E2: names are allowed (and required) together with a linked profile.
        InstitutionAccountRules.Validate(Request(studentId: Guid.NewGuid()));
        InstitutionAccountRules.Validate(Request([InstitutionRole.Teacher], teacherId: Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => InstitutionAccountRules.Validate(Request(studentId: Guid.NewGuid(), classId: Guid.NewGuid())));
        Assert.Throws<ArgumentException>(() => InstitutionAccountRules.Validate(Request([InstitutionRole.Teacher], classId: Guid.NewGuid())));
        Assert.Throws<ArgumentException>(() => InstitutionAccountRules.Validate(Request([InstitutionRole.Student, InstitutionRole.Teacher])));
        Assert.Throws<ArgumentException>(() => InstitutionAccountRules.Validate(Request([InstitutionRole.Staff], studentId: Guid.NewGuid())));
        Assert.Throws<ArgumentException>(() => InstitutionAccountRules.Validate(Request(first: new string('a', 101))));
    }
}
