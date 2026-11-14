using ElectronicRegisterAPI.Domain.Configuration;

namespace ElectronicRegisterAPI.Application.Services;

// BCrypt is CPU-bound: the hashes of an import are computed in parallel, but never on more
// threads than the process has cores, and the budget is shared by every import running in the
// process, so several confirmations at once cannot occupy all cores and starve other requests.
internal sealed class ImportHashBudget
{
    private readonly SemaphoreSlim _gate;

    public ImportHashBudget(PlatformIdentitySettings settings)
        : this(settings.ImportHashParallelism, Environment.ProcessorCount)
    {
    }

    internal ImportHashBudget(int configured, int cores)
    {
        Degree = Math.Clamp(configured > 0 ? configured : cores, 1, Math.Min(Math.Max(cores, 1), PlatformIdentitySettings.MaximumImportHashParallelism));
        _gate = new(Degree, Degree);
    }

    public int Degree { get; }

    public string[] Hash(IReadOnlyList<string> secrets, Func<string, string> hash, CancellationToken ct)
    {
        var hashes = new string[secrets.Count];
        Parallel.For(0, secrets.Count, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Degree }, i =>
        {
            _gate.Wait(ct);
            try
            {
                hashes[i] = hash(secrets[i]);
            }
            finally
            {
                _gate.Release();
            }
        });
        return hashes;
    }
}
