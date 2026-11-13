using System;
using System.IO;

namespace ElectronicRegisterAPI.Tests.Integration;

// Explicit alternate instance for disposable runs. No discovery or fallback.
internal static class TestMySqlTarget
{
    internal static uint Port
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_PORT");
            if (value is null) return 13306;
            if (!uint.TryParse(value, out var port) || port is < 1024 or > 65535 or 3306 or 13306)
                throw new InvalidOperationException("Invalid disposable test port");
            _ = DataDirectorySuffix;
            return port;
        }
    }

    internal static string DataDirectorySuffix
    {
        get
        {
            if (Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_PORT") is null)
                return "/migrations/mt4/private/mt5a-mamp-13306/data/";
            var path = Path.GetFullPath(Environment.GetEnvironmentVariable("ER_TEST_ISOLATED_DATADIR") ?? throw new InvalidOperationException("Explicit disposable datadir required"));
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "migrations", "bootstrap", "001_schema.sql"))) root = root.Parent;
            if (root is null || !path.StartsWith(Path.Combine(root.FullName, ".staff-private") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Disposable datadir outside workspace");
            for (var directory = new DirectoryInfo(path); directory is not null && directory.FullName.StartsWith(root.FullName, StringComparison.OrdinalIgnoreCase); directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Disposable datadir cannot use reparse points");
            return path.TrimEnd('\\', '/').Replace('\\', '/') + "/";
        }
    }
}
