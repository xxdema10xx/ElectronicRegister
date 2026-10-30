internal static class NamespacePath
{
    public static string ExpectedNamespace(string rootNamespace, string relativeDirectory)
    {
        // Handle both path conventions regardless of the OS running the guard.
        var suffix = relativeDirectory.Replace('\\', '.').Replace('/', '.');
        return (rootNamespace + "." + suffix).TrimEnd('.');
    }
}
