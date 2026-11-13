using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ElectronicRegisterAPI.Domain.Policies;

// Platform address grammar: nome.cognome[N]@domain, lowercase ASCII, deterministic.
// Candidate 1 is the bare base; candidate n >= 2 appends n. Local part <= 64 characters
// including the suffix, full address <= 254. Never an opaque identifier.
public static class PlatformEmailRules
{
    public const int MaxLocalLength = 64;
    public const int MaxAddressLength = 254;
    public const int MaxDomainLength = 189;
    public const int MaxCandidates = 9999;
    private const string Apostrophes = "'’‘`´ʼ";
    private const string Separators = "-_.‐‑–—";
    private static readonly Regex Domain = new("^(?=.{1,189}$)[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)+$", RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<char, string> Letters = new Dictionary<char, string>
    {
        ['ß'] = "ss",
        ['æ'] = "ae",
        ['Æ'] = "ae",
        ['œ'] = "oe",
        ['Œ'] = "oe",
        ['ø'] = "o",
        ['Ø'] = "o",
        ['ł'] = "l",
        ['Ł'] = "l",
        ['đ'] = "d",
        ['Đ'] = "d",
        ['ð'] = "d",
        ['Ð'] = "d",
        ['þ'] = "th",
        ['Þ'] = "th",
        ['ı'] = "i"
    };

    public static bool IsValidDomain(string? domain) => domain is not null && Domain.IsMatch(domain);

    // One name component (first or last name) to its dotted ASCII form; "" when nothing remains.
    public static string NormalizePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim().Normalize(NormalizationForm.FormKD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                continue;
            if (Apostrophes.Contains(c))
                continue;
            if (Letters.TryGetValue(c, out var mapped))
            {
                builder.Append(mapped);
                continue;
            }
            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
                builder.Append(lower);
            else if (char.IsWhiteSpace(c) || Separators.Contains(c))
                builder.Append('.');
        }
        return Collapse(builder.ToString());
    }

    // Base local part "first.last" before any suffix; null when a name has no representable letter.
    public static string? Base(string? firstName, string? lastName)
    {
        var first = NormalizePart(firstName);
        var last = NormalizePart(lastName);
        return first.Length == 0 || last.Length == 0 ? null : first + "." + last;
    }

    public static string Candidate(string localBase, int number, string domain)
    {
        if (number is < 1 or > MaxCandidates || string.IsNullOrEmpty(localBase) || !IsValidDomain(domain))
            throw new ArgumentException("Candidato email non valido.");
        var suffix = number == 1 ? "" : number.ToString(CultureInfo.InvariantCulture);
        var head = Head(localBase, suffix.Length);
        var address = head + suffix + "@" + domain;
        if (head.Length == 0 || address.Length > MaxAddressLength)
            throw new ArgumentException("Candidato email non valido.");
        return address;
    }

    // Prefix shared by every candidate of a base, used for bulk reservation lookups.
    public static string LookupPrefix(string localBase) => Head(localBase, 4);

    private static string Head(string localBase, int suffixLength) =>
        localBase.Length + suffixLength > MaxLocalLength ? localBase[..(MaxLocalLength - suffixLength)].TrimEnd('.') : localBase;

    private static string Collapse(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
            if (c != '.' || builder.Length > 0 && builder[^1] != '.')
                builder.Append(c);
        return builder.ToString().Trim('.');
    }
}
