using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// Filesystem/ACL checks only: no MySQL fixture, schema or HTTP host is resolved.
[SupportedOSPlatform("windows")]
public sealed class PrivateTestFilesTests
{
    [Fact]
    public void EachFixtureHasItsOwnPrivateDirectoryOutsideDatabaseDatadirs()
    {
        var first = PrivateTestFiles.CreateDirectory("isolation-probe");
        var second = PrivateTestFiles.CreateDirectory("isolation-probe");
        Assert.NotEqual(first, second);
        var run = Directory.GetParent(first)!;
        Assert.Equal(run.FullName, Directory.GetParent(second)!.FullName);
        Assert.StartsWith("cli-fixtures-", run.Name);
        Assert.Equal(".staff-private", run.Parent!.Name);
        Assert.DoesNotContain("mt5a-mamp-13306", first);
        AssertPrivateAcl(run);
        AssertPrivateAcl(new DirectoryInfo(first));
        AssertPrivateAcl(new DirectoryInfo(second));

        var file = Path.Combine(first, "synthetic.json");
        File.WriteAllText(file, "{}");
        AssertTrustedRules(new FileInfo(file).GetAccessControl());
    }

    [Fact]
    public void EveryoneReadIsActuallyAppliedAndCanBeRestoredOnTheNewFileOnly()
    {
        var folder = PrivateTestFiles.CreateDirectory("acl-probe");
        var run = Directory.GetParent(folder)!;
        var parentDescriptor = run.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All);
        var file = new FileInfo(Path.Combine(folder, "synthetic.json"));
        File.WriteAllText(file.FullName, "{}");
        var original = file.GetAccessControl();
        var originalDescriptor = original.GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner);
        var readable = file.GetAccessControl();
        readable.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-1-0"), FileSystemRights.ReadData, AccessControlType.Allow));
        try
        {
            file.SetAccessControl(readable);
            Assert.Contains(file.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>(),
                r => r.IdentityReference.Value == "S-1-1-0" && r.AccessControlType == AccessControlType.Allow &&
                    (r.FileSystemRights & FileSystemRights.ReadData) != 0);
        }
        finally { PrivateTestFiles.RestoreAccessRules(file.FullName, original); }

        Assert.Equal(originalDescriptor, file.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access | AccessControlSections.Owner));
        Assert.Equal(parentDescriptor, run.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All));
        AssertTrustedRules(file.GetAccessControl());
    }

    private static void AssertPrivateAcl(DirectoryInfo directory)
    {
        var acl = directory.GetAccessControl();
        Assert.True(acl.AreAccessRulesProtected);
        AssertTrustedRules(acl);
    }

    private static void AssertTrustedRules(FileSystemSecurity acl)
    {
        var trusted = new[] { WindowsIdentity.GetCurrent().User!.Value, "S-1-5-18", "S-1-5-32-544" };
        Assert.Contains(acl.GetOwner(typeof(SecurityIdentifier))!.Value, trusted);
        Assert.All(acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
            .Where(r => r.AccessControlType == AccessControlType.Allow), r => Assert.Contains(r.IdentityReference.Value, trusted));
    }
}
