using ElectronicRegisterAPI.Domain.Enums;
using System;
using System.Linq;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public class InstitutionEmailGeneratorTests
{
    private readonly InstitutionEmailGenerator engine = new();
    private static EmailTemplate Policy(string template = "allievo_{cognome}.{nome:iniziale}", string domain = "itsumbria.it") =>
        new(template, domain, new());
    [Theory]
    [InlineData("Mario", "Rossi", "allievo_rossi.m@itsumbria.it")]
    [InlineData("Giulia", "Bianchi", "allievo_bianchi.g@itsumbria.it")]
    [InlineData("Luca", "De Luca", "allievo_deluca.l@itsumbria.it")]
    [InlineData("Anna Maria", "D'Angelo", "allievo_dangelo.a@itsumbria.it")]
    [InlineData("José", "García", "allievo_garcia.j@itsumbria.it")]
    [InlineData("Jean-Pierre", "Dupont", "allievo_dupont.j@itsumbria.it")]
    [InlineData("  Anna   Maria ", " De   Luca ", "allievo_deluca.a@itsumbria.it")]
    [InlineData("Mario", "D’Angelo", "allievo_dangelo.m@itsumbria.it")]
    public void UmbriaNamesAreNormalizedByExplicitPolicy(string first, string last, string expected) =>
        Assert.Equal(expected, engine.Preview(Policy(), new(first, last)).Email);

    [Theory]
    [InlineData("{nome}.{cognome}", "mario.rossi")]
    [InlineData("{cognome}.{nome}", "rossi.mario")]
    [InlineData("{nome:iniziale}.{cognome}", "m.rossi")]
    [InlineData("{nome}.{cognome:iniziale}", "mario.r")]
    [InlineData("studente_{nome}_{cognome}", "studente_mario_rossi")]
    [InlineData("{cognome}{nome:iniziale}", "rossim")]
    [InlineData("docente-{cognome}-{nome}", "docente-rossi-mario")]
    [InlineData("staff.{nome}.{cognome}", "staff.mario.rossi")]
    [InlineData("x+{nome:primi(3)}_{cognome:primi(2)}-2027", "x+mar_ro-2027")]
    [InlineData("{nome:maiuscolo}.{cognome:minuscolo}", "MARIO.rossi")]
    [InlineData("{nome:primi(3):maiuscolo}.{cognome:iniziale}", "MAR.r")]
    [InlineData("mailbox", "mailbox")]
    [InlineData("{{{nome}}}", "{mario}")]
    [InlineData("a!#$%&'*+-/=?^_`|~{nome}", "a!#$%&'*+-/=?^_`|~mario")]
    public void ArbitraryCompositionsAreConfiguration(string template, string expected) =>
        Assert.Equal(expected + "@academy.example.it", engine.Preview(Policy(template, "academy.example.it"), new("Mario", "Rossi")).Email);

    [Theory]
    [InlineData(".", "anna.maria.de.luca")]
    [InlineData("_", "anna_maria.de_luca")]
    [InlineData("-", "anna-maria.de-luca")]
    [InlineData("", "annamaria.deluca")]
    public void WordSeparatorIsNotUniversallyRemoved(string separator, string expected)
    {
        var p = Policy("{nome}.{cognome}") with { Names = new(WordSeparator: separator) };
        Assert.Equal(expected + "@itsumbria.it", engine.Preview(p, new("Anna Maria", "De Luca")).Email);
    }

    [Fact]
    public void HyphenAndApostropheBoundaryArePolicyChoices()
    {
        var p = Policy("{nome}.{cognome}") with { Names = new(WordSeparator: "-", Punctuation: EmailPunctuationHandling.WordBoundary) };
        Assert.Equal("jean-pierre.d-angelo@itsumbria.it", engine.Preview(p, new("Jean-Pierre", "D'Angelo")).Email);
        Assert.Equal("jeanpierre.dangelo@itsumbria.it", engine.Preview(p with { Names = new() }, new("Jean-Pierre", "D'Angelo")).Email);
    }

    [Theory]
    [InlineData("{unknown}")][InlineData("{nome")][InlineData("nome}")][InlineData("{nome{cognome}")]
    [InlineData("{nome:eval}")][InlineData("{nome:primi(0)}")][InlineData("{nome:primi(-1)}")]
    [InlineData("{nome:primi(65)}")][InlineData("{nome:primi(01)}")][InlineData("{nome:primi(1.5)}")]
    [InlineData("{nome:primi( 2)}")][InlineData("{nome:}")][InlineData("{Nome}")]
    [InlineData("{nome.ToString()}")][InlineData("{nome:regex(.*)}")][InlineData("{User.Email}")]
    [InlineData("")][InlineData(".abc")][InlineData("abc.")][InlineData("a..b")]
    [InlineData("{nome} {cognome}")][InlineData("{nome}@other.it")][InlineData("\n{nome}")]
    public void MalformedTemplateIsRejectedStructurally(string template) => Assert.Throws<ArgumentException>(() => engine.Validate(Policy(template)));

    [Theory]
    [InlineData("https://itsumbria.it")][InlineData("itsumbria.it:443")][InlineData("itsumbria.it/path")]
    [InlineData("a@itsumbria.it")][InlineData("-bad.it")][InlineData("bad-.it")]
    [InlineData("a..it")][InlineData("localhost")][InlineData(".it")][InlineData("it.")]
    [InlineData("español.it")][InlineData(" good.it")][InlineData("")]
    public void InvalidDomainsCannotBePublished(string domain) => Assert.Throws<ArgumentException>(() => engine.Validate(Policy(domain: domain)));

    [Theory]
    [InlineData("")][InlineData("   ")][InlineData("---''")][InlineData("王")][InlineData("Иван")]
    [InlineData("\u0000Mario")][InlineData("Mario\n")][InlineData("Mario\tRossi")]
    [InlineData("Ma\u200brio")][InlineData("😀")]
    public void UnusableNamesRequireReview(string first) => Assert.Throws<ArgumentException>(() => engine.Preview(Policy(), new(first, "Rossi")));

    [Theory]
    [InlineData(0xd800)][InlineData(0xdc00)]
    public void UnpairedSurrogateRequiresReview(int codeUnit) =>
        Assert.Throws<ArgumentException>(()=>engine.Preview(Policy(),new(new string((char)codeUnit,1),"Rossi")));

    [Fact]
    public void ComposedAndDecomposedUnicodeAreDeterministic()
    {
        var p = Policy("{nome}.{cognome}");
        var a = engine.Preview(p, new("José", "García"));
        Assert.Equal(a, engine.Preview(p, new("José", "García")) with { Warnings = a.Warnings });
        Assert.Equal(a.Email, engine.Preview(p, new("Jose\u0301", "Garci\u0301a")).Email);
        Assert.Equal("jose.garcia@itsumbria.it", a.NormalizedEmail);
        Assert.Throws<ArgumentException>(() => engine.Preview(p with { Names = new(Accents: EmailAccentHandling.RejectNonAscii) }, new("José", "García")));
    }

    [Fact]
    public void BoundariesAndOutputAreValidatedForEveryInput()
    {
        Assert.Throws<ArgumentException>(() => engine.Preview(Policy("{nome}"), new(new string('a', 65), "Rossi")));
        Assert.Throws<ArgumentException>(() => engine.Preview(Policy(), new(new string('a', 257), "Rossi")));
        Assert.Throws<ArgumentException>(() => engine.Validate(Policy(new string('a', 513))));
        Assert.Throws<ArgumentException>(() => engine.Validate(Policy(string.Concat(Enumerable.Repeat("{nome:iniziale}", 33)))));
        Assert.Throws<ArgumentException>(() => engine.Preview(Policy("{nome:primi(5)}") with { Names = new(WordSeparator: ".") }, new("Anna Maria", "Rossi")));
        Assert.Equal(64, engine.Preview(Policy("{nome}"), new(new string('a', 64), "Rossi")).LocalPart.Length);
    }

    [Fact]
    public void NumericSuffixValidationUsesOnlyReachableCandidates()
    {
        var p=Policy(new string('x',63)) with {Collision=EmailCollisionStrategy.NumericSuffix,NumericSuffix="{n}",SuffixStart=9,MaximumAttempts=2};
        Assert.Equal(new string('x',63)+"9@itsumbria.it",engine.Candidate(p,new("Mario","Rossi"),1).Email);
        engine.Validate(p with {MaximumAttempts=1});
        Assert.Throws<ArgumentException>(()=>engine.Validate(p with {MaximumAttempts=3}));
    }

    [Fact]
    public void SuffixCandidatesHaveSafeConfiguredGrammarButDoNotClaimAvailability()
    {
        var p = Policy() with { Collision = EmailCollisionStrategy.NumericSuffix, NumericSuffix = "_{n}", MaximumAttempts = 4 };
        Assert.Equal("allievo_rossi.m@itsumbria.it", engine.Candidate(p, new("Mario", "Rossi"), 0).Email);
        Assert.Equal("allievo_rossi.m_2@itsumbria.it", engine.Candidate(p, new("Mario", "Rossi"), 1).Email);
        Assert.Equal("allievo_rossi.m_4@itsumbria.it", engine.Candidate(p, new("Mario", "Rossi"), 3).Email);
        Assert.Throws<ArgumentException>(() => engine.Candidate(p, new("Mario", "Rossi"), 4));
        Assert.Throws<ArgumentException>(() => engine.Validate(p with { NumericSuffix = "{n}{n}" }));
        Assert.Throws<ArgumentException>(() => engine.Validate(p with { NumericSuffix = "{nome}" }));
        Assert.Throws<ArgumentException>(() => engine.Validate(p with { NumericSuffix = ".{n}." }));
        Assert.Throws<ArgumentException>(() => engine.Candidate(Policy(), new("Mario", "Rossi"), 1));
    }

    [Theory]
    [InlineData("evil@other.it")][InlineData("a..b@itsumbria.it")][InlineData(" a@itsumbria.it")]
    [InlineData("a@itsumbria.it\n")][InlineData("a@@itsumbria.it")][InlineData("a@https://itsumbria.it")]
    public void OverridesMustMatchPolicyDomainAndValidAddress(string address) =>
        Assert.Throws<ArgumentException>(() => engine.ValidateAddress(address, "itsumbria.it"));

    [Fact]
    public void PreviewNeverMeansProvisionedVerifiedOrCredential()
    {
        var result = engine.Preview(Policy(), new("Mario", "Rossi"));
        Assert.Single(result.Warnings);
        Assert.Contains("nessuna mailbox", result.Warnings[0]);
        Assert.Equal("mario.rossi@itsumbria.it", engine.ValidateAddress("Mario.Rossi@ITSUMBRIA.IT", "itsumbria.it"));
    }
}
