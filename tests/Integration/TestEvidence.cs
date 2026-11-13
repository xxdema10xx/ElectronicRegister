using System;
using System.IO;

namespace ElectronicRegisterAPI.Tests.Integration;

// Runtime observations written by the suite (SQL captures, route tables) go to
// the build output, which git ignores (bin/), the release closure excludes and
// no CI job uploads. The versioned files under tests/Integration/TestResults/
// remain the historical, reviewed evidence and are never rewritten by a run.
internal static class TestEvidence
{
    internal static string Directory(string area)
    {
        if (string.IsNullOrWhiteSpace(area) || area.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || area.Contains(".."))
            throw new ArgumentException("Explicit evidence area required", nameof(area));
        var path = Path.Combine(AppContext.BaseDirectory, "test-evidence", area);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }
}
