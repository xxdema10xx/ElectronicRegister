using ElectronicRegisterAPI.Api.Controllers;
using System.Globalization;

namespace ElectronicRegisterAPI.Api.Http;

internal static class InstitutionAcademicHttp
{
    internal static void Query(HttpRequest request)
    {
        if (request.Query.Keys.Any(k => !k.Equals("pageNumber", StringComparison.OrdinalIgnoreCase) && !k.Equals("pageSize", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Filtro non supportato.");
    }

    internal static long? Revision(HttpRequest request)
    {
        var s = request.Headers.IfMatch.ToString();
        if (s.Length == 0)
            return null;
        if (s.Length < 3 || s[0] != '"' || s[^1] != '"' || !long.TryParse(s.AsSpan(1, s.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var r))
            throw new ArgumentException("If-Match non valido.");
        return r;
    }
}
