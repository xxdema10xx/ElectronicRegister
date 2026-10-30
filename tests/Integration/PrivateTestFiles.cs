using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ElectronicRegisterAPI.Tests.Integration;

internal static class PrivateTestFiles
{
    private static readonly Lazy<string> RunDirectory = new(CreateRunDirectory);

    internal static string CreateDirectory(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || label is "." or ".." || label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("A single fixture directory label is required.", nameof(label));
        var path = Path.Combine(RunDirectory.Value, label + "-" + Guid.NewGuid().ToString("N"));
        CreateNewProtectedDirectory(path);
        return path;
    }

    internal static void RestoreAccessRules(string path, FileSecurity original)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Protected Windows administrative host required");
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(RunDirectory.Value + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only files in this fixture run may have their ACL restored.", nameof(path));
        RejectReparsePoints(Path.GetDirectoryName(fullPath)!);
        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Fixture files cannot be reparse points.");
        // A freshly read FileSecurity has no modified sections. Mark the DACL
        // explicitly so SetAccessControl actually removes the temporary grant.
        var restore = new FileSecurity();
        restore.SetSecurityDescriptorSddlForm(original.GetSecurityDescriptorSddlForm(AccessControlSections.Access), AccessControlSections.Access);
        new FileInfo(fullPath).SetAccessControl(restore);
    }

    private static string CreateRunDirectory()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "migrations", "bootstrap", "001_schema.sql")))
            root = root.Parent;
        if (root is null)
            throw new InvalidOperationException("Cannot locate the fixture workspace.");
        // A sibling of instance-* datadirs, never a child of a database directory.
        var path = Path.Combine(root.FullName, ".staff-private", "cli-fixtures-" + Guid.NewGuid().ToString("N"));
        CreateNewProtectedDirectory(path);
        return path;
    }

    private static void CreateNewProtectedDirectory(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Protected Windows administrative host required");
        if (Directory.Exists(path) || File.Exists(path))
            throw new IOException("A fixture directory must be new.");
        RejectReparsePoints(path);
        Directory.CreateDirectory(path);
        RejectReparsePoints(path);
        ProtectNewDirectory(path);
    }

    private static void RejectReparsePoints(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Fixture paths cannot traverse reparse points.");
    }

    private static void ProtectNewDirectory(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Protected Windows administrative host required");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { WindowsIdentity.GetCurrent().User!.Value, "S-1-5-18", "S-1-5-32-544" })
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }
}
