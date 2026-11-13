using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Policies;

namespace ElectronicRegisterAPI.Domain.Models;

// In-memory allocation over a snapshot of reserved addresses read under the global email
// mutex. Each Take returns the first never-issued candidate of a base and marks it taken,
// so homonyms in one batch receive deterministic suffixes in call order. A 1062 from a
// writer outside the mutex is handled by MarkTaken followed by another Take.
public sealed class PlatformEmailPlan
{
    private readonly HashSet<string> _taken;

    public PlatformEmailPlan(string domain, IEnumerable<string> reserved)
    {
        if (!PlatformEmailRules.IsValidDomain(domain))
            throw new ArgumentException("Dominio di piattaforma non valido.");
        Domain = domain;
        _taken = new HashSet<string>(reserved.Select(a => a.ToLowerInvariant()), StringComparer.Ordinal);
    }

    public string Domain { get; }

    public string Take(string localBase)
    {
        for (var n = 1; n <= PlatformEmailRules.MaxCandidates; n++)
        {
            var candidate = PlatformEmailRules.Candidate(localBase, n, Domain);
            if (_taken.Add(candidate))
                return candidate;
        }
        throw new AccountEmailUnavailableException();
    }

    public void MarkTaken(string address) => _taken.Add(address.ToLowerInvariant());
}
