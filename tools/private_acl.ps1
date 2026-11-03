# Only ACL metadata is emitted; never enumerate file contents or environment.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$p = $env:ER_SETUP_PRIVATE_PATH
$operation = 'identity'
$result = @{status='FAIL'; operation=$operation; snapshots=@{}}
function Snapshot($path) {
    $acl = Get-Acl -LiteralPath $path
    return @{owner=$acl.GetOwner([Security.Principal.SecurityIdentifier]).Value;
        group=$acl.GetGroup([Security.Principal.SecurityIdentifier]).Value;
        protected=$acl.AreAccessRulesProtected; sddl=$acl.Sddl;
        rules=@($acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]) | ForEach-Object {
            @{sid=$_.IdentityReference.Value; rights=[int]$_.FileSystemRights;
              type=[string]$_.AccessControlType; inherited=$_.IsInherited;
              inheritance=[string]$_.InheritanceFlags; propagation=[string]$_.PropagationFlags}})}
}
try {
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $trusted = @($sid.Value,'S-1-5-18','S-1-5-32-544')
    $result.user = $sid.Value
    $operation = 'read-target'
    $result.snapshots.before = Snapshot $p
    $operation = 'read-parent'
    $result.snapshots.parent = Snapshot ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($p)))
    # Start with the existing descriptor: preserve group, and avoid submitting
    # an uninitialized owner/group through the PowerShell security provider.
    $operation = 'prepare-dacl'
    $a = Get-Acl -LiteralPath $p
    $a.SetAccessRuleProtection($true,$false)
    foreach($rule in @($a.GetAccessRules($true,$false,[Security.Principal.SecurityIdentifier]))) {
        [void]$a.RemoveAccessRuleSpecific($rule)
    }
    foreach($id in $trusted) {
        $s = New-Object Security.Principal.SecurityIdentifier($id)
        $r = New-Object Security.AccessControl.FileSystemAccessRule($s,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
        [void]$a.AddAccessRule($r)
    }
    $operation = 'set-owner'
    if($a.GetOwner([Security.Principal.SecurityIdentifier]).Value -ne $sid.Value) { $a.SetOwner($sid) }
    # Set-Acl copies the whole descriptor, then guesses a smaller section set
    # on privilege failure. Its audit/protection heuristic can still request
    # SeSecurityPrivilege on an already protected directory.
    # The .NET writer persists only modified DACL/owner sections; never SACL.
    $operation = 'Directory.SetAccessControl'
    [IO.Directory]::SetAccessControl($p,$a)
    $operation = 'verify-sid-dacl'
    $after = Snapshot $p
    $result.snapshots.after = $after
    if(!$after.protected -or $after.owner -ne $sid.Value -or $after.rules.Count -ne 3) { throw 'Private owner/protection/rule count mismatch' }
    foreach($id in $trusted) {
        $rules = @($after.rules | Where-Object { $_.sid -eq $id })
        if($rules.Count -ne 1 -or $rules[0].type -ne 'Allow' -or $rules[0].inherited -or
           $rules[0].rights -ne [int][Security.AccessControl.FileSystemRights]::FullControl -or
           $rules[0].inheritance -ne 'ContainerInherit, ObjectInherit' -or $rules[0].propagation -ne 'None') { throw 'Unexpected private DACL' }
    }
    $result.status = 'PASS'
} catch {
    $failure = $_
    $result.error = @{message=$failure.Exception.Message.Replace($p,'<target>');
        type=$failure.Exception.GetType().FullName; hresult=$failure.Exception.HResult;
        id=$failure.FullyQualifiedErrorId; category=[string]$failure.CategoryInfo.Category}
    try { $result.snapshots.failure = Snapshot $p } catch { $result.snapshotUnavailable = $true }
} finally {
    $result.operation = $operation
    $result | ConvertTo-Json -Depth 9 -Compress
}
if($result.status -ne 'PASS') { exit 1 }
