using System.Globalization;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

/// <summary>Bounded, linear parser/interpolator. No eval, user regex, reflection, IO or lookup.</summary>
public sealed class InstitutionEmailGenerator : IInstitutionEmailGenerator
{
    private sealed record Part(string Text, string? Field = null, string[]? Transforms = null);
    private const string Atext = "!#$%&'*+-/=?^_`{|}~";
    private static ArgumentException Invalid(string detail) => new("Configurazione email non valida: " + detail);
    private static bool Atom(char c) => char.IsAsciiLetterOrDigit(c) || Atext.Contains(c);

    public void Validate(EmailTemplate t)
    {
        if (t is null || t.Names is null || !Enum.IsDefined(t.Names.Case) || !Enum.IsDefined(t.Names.Accents) ||
            !Enum.IsDefined(t.Names.Punctuation) || !Enum.IsDefined(t.Collision) ||
            t.Names.WordSeparator is not ("" or "." or "-" or "_") ||
            t.SuffixStart is < 2 or > 9999 || t.MaximumAttempts is < 1 or > 100)
            throw Invalid("opzioni fuori intervallo");
        _ = Domain(t.Domain);
        _ = Parse(t.LocalPartTemplate);
        if (t.NumericSuffix is null || t.NumericSuffix.Length is < 3 or > 24 ||
            t.NumericSuffix.IndexOf("{n}", StringComparison.Ordinal) < 0 ||
            t.NumericSuffix.Replace("{n}", "", StringComparison.Ordinal).IndexOfAny(['{', '}']) >= 0 ||
            t.NumericSuffix.Split("{n}", StringSplitOptions.None).Length != 2 ||
            t.NumericSuffix.Replace("{n}", "2", StringComparison.Ordinal).Any(c => !Atom(c) && c != '.'))
            throw Invalid("suffisso numerico: un solo {n}, caratteri dot-atom, massimo 24 caratteri");
        // Structural proof is independent of one example name: expansions are nonempty
        // and bounded; literal leading/trailing/double dots are never repairable.
        var shape = string.Concat(Parse(t.LocalPartTemplate).Select(p => p.Field is null ? p.Text : "x"));
        Local(shape);
        if (t.Collision == EmailCollisionStrategy.NumericSuffix && t.MaximumAttempts > 1)
            Local(shape + t.NumericSuffix.Replace("{n}", (t.SuffixStart + t.MaximumAttempts - 2).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    public GeneratedInstitutionEmail Preview(EmailTemplate t, EmailGenerationInput input) => Candidate(t, input, 0);
    public GeneratedInstitutionEmail Candidate(EmailTemplate t, EmailGenerationInput input, int attempt)
    {
        Validate(t);
        if (input is null || attempt < 0 || attempt >= t.MaximumAttempts ||
            attempt > 0 && t.Collision != EmailCollisionStrategy.NumericSuffix) throw Invalid("tentativo non consentito");
        var output = new StringBuilder(512);
        foreach (var p in Parse(t.LocalPartTemplate))
        {
            if (p.Field is null) { output.Append(p.Text); continue; }
            var value = Normalize(p.Field == "nome" ? input.FirstName : input.LastName, t.Names);
            foreach (var transform in p.Transforms!)
            {
                value = transform switch
                {
                    "iniziale" => value[..1], "minuscolo" => value.ToLowerInvariant(), "maiuscolo" => value.ToUpperInvariant(),
                    _ => value[..Math.Min(value.Length, PrefixLength(transform))]
                };
            }
            output.Append(value);
        }
        if (attempt > 0) output.Append(t.NumericSuffix.Replace("{n}", (t.SuffixStart + attempt - 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
        var local = output.ToString(); Local(local);
        var domain = Domain(t.Domain);
        var email = local + "@" + domain;
        if (email.Length > 254) throw Invalid("indirizzo oltre 254 ottetti ASCII");
        return new(email, email.ToLowerInvariant(), local, domain,
            ["Indirizzo generato: dominio non verificato, nessuna mailbox provisionata o verificata, nessuna credenziale creata."]);
    }

    public string ValidateAddress(string address, string requiredDomain)
    {
        if (string.IsNullOrEmpty(address) || address.Length > 254 || address.Count(c => c == '@') != 1)
            throw Invalid("indirizzo non valido");
        var split = address.Split('@'); Local(split[0]);
        if (Domain(split[1]) != Domain(requiredDomain)) throw Invalid("dominio diverso dalla policy");
        return address.ToLowerInvariant();
    }

    private static int PrefixLength(string value)
    {
        if (!value.StartsWith("primi(", StringComparison.Ordinal) || !value.EndsWith(')')) throw Invalid("trasformazione sconosciuta");
        var n = value.AsSpan(6, value.Length - 7);
        if (n.Length is < 1 or > 2 || n[0] == '0' || !int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var length) || length is < 1 or > 64)
            throw Invalid("primi(N): N intero da 1 a 64");
        return length;
    }

    private static List<Part> Parse(string source)
    {
        if (string.IsNullOrEmpty(source) || source.Length > 512) throw Invalid("template da 1 a 512 caratteri");
        var parts = new List<Part>(); var literal = new StringBuilder(); var tokens = 0;
        void Flush() { if (literal.Length > 0) { parts.Add(new(literal.ToString())); literal.Clear(); } }
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c is '{' or '}' && i + 1 < source.Length && source[i + 1] == c) { literal.Append(c); i++; continue; }
            if (c == '}') throw Invalid("delimitatore non bilanciato");
            if (c != '{') { if (!Atom(c) && c != '.') throw Invalid("carattere letterale non ammesso"); literal.Append(c); continue; }
            Flush(); var end = source.IndexOf('}', i + 1);
            if (end < 0 || ++tokens > 32) throw Invalid("delimitatore non bilanciato o oltre 32 placeholder");
            var segments = source[(i + 1)..end].Split(':');
            if (segments[0] is not ("nome" or "cognome") || segments.Length > 5) throw Invalid("placeholder o numero trasformazioni non valido");
            foreach (var transform in segments.Skip(1))
                if (transform is not ("iniziale" or "minuscolo" or "maiuscolo")) _ = PrefixLength(transform);
            parts.Add(new("", segments[0], segments.Skip(1).ToArray())); i = end;
        }
        Flush(); return parts;
    }

    private static string Normalize(string? raw, EmailNamingRules rules)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 256) throw Invalid("nome/cognome vuoto o oltre 256 caratteri");
        // Reject malformed UTF-16 and format/control characters before normalization.
        for (var i = 0; i < raw.Length; i++)
        {
            if (char.IsSurrogate(raw[i])) { if (!char.IsHighSurrogate(raw[i]) || i + 1 >= raw.Length || !char.IsLowSurrogate(raw[++i])) throw Invalid("Unicode malformato"); }
        }
        var text = rules.Accents == EmailAccentHandling.LatinFoldV1 ? raw.Normalize(NormalizationForm.FormD) : raw;
        var output = new StringBuilder(256); var boundary = false;
        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format) throw Invalid("carattere di controllo/formato");
            if (rules.Accents == EmailAccentHandling.LatinFoldV1 && category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark) continue;
            if (Rune.IsWhiteSpace(rune) || rune.Value is '-' or '\'' or 0x2019)
            {
                if (Rune.IsWhiteSpace(rune) || rules.Punctuation == EmailPunctuationHandling.WordBoundary) boundary = output.Length > 0;
                continue;
            }
            var piece = rune.IsAscii && char.IsAsciiLetterOrDigit((char)rune.Value) ? rune.ToString() :
                rules.Accents == EmailAccentHandling.LatinFoldV1 ? rune.Value switch
                { 0xDF => "ss", 0xC6 or 0xE6 => "ae", 0xD8 or 0xF8 => "o", 0x141 or 0x142 => "l", 0x152 or 0x153 => "oe", _ => throw Invalid("nome non convertibile con LatinFoldV1") }
                : throw Invalid("nome non ASCII");
            if (boundary) output.Append(rules.WordSeparator);
            boundary = false; output.Append(piece);
        }
        if (output.Length == 0) throw Invalid("nome senza componenti utilizzabili");
        return rules.Case == EmailNameCase.Lower ? output.ToString().ToLowerInvariant() : output.ToString().ToUpperInvariant();
    }

    private static string Domain(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length > 253 || raw.Contains('@')) throw Invalid("dominio non valido");
        var labels = raw.Split('.');
        if (labels.Length < 2 || labels.Any(l => l.Length is < 1 or > 63 || !char.IsAsciiLetterOrDigit(l[0]) ||
            !char.IsAsciiLetterOrDigit(l[^1]) || l.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')))
            throw Invalid("dominio DNS ASCII richiesto (IDN solo A-label/punycode esplicito); nessun URL, porta o percorso");
        return raw.ToLowerInvariant();
    }
    private static void Local(string value)
    {
        if (value.Length is < 1 or > 64 || value[0] == '.' || value[^1] == '.' || value.Contains("..", StringComparison.Ordinal) || value.Any(c => !Atom(c) && c != '.'))
            throw Invalid("local-part dot-atom ASCII da 1 a 64 ottetti, senza punti adiacenti/estremi");
    }
}
