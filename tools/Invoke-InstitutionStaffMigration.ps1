[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MySqlBin,
    [Parameter(Mandatory=$true)][int]$Port,
    [Parameter(Mandatory=$true)][string]$ExpectedDataDirectory,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
    [switch]$Apply
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$private=[IO.Path]::GetFullPath((Join-Path $root '.staff-private'))+[IO.Path]::DirectorySeparatorChar
$data=[IO.Path]::GetFullPath($ExpectedDataDirectory).TrimEnd('\','/')
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
if($Port -lt 1024 -or $Port -gt 65535 -or $Port -in @(3306,13306)){throw 'A new dedicated test port is required'}
if(!$data.StartsWith($private,[StringComparison]::OrdinalIgnoreCase) -or !$evidence.StartsWith($private,[StringComparison]::OrdinalIgnoreCase)){throw 'Only disposable workspace Staff instances are supported'}
if(Test-Path -LiteralPath $evidence){throw 'Evidence already exists; preserve state and do not replay'}
$client=Join-Path $MySqlBin 'mysql.exe'
$arguments=@('--no-defaults','--protocol=TCP','--host=127.0.0.1',"--port=$Port",'--user=root','--database=electronic_register_test','--batch','--raw','--skip-column-names','--default-character-set=utf8mb4')
function Query([string]$sql){
    $result=$sql | & $client @arguments
    if($LASTEXITCODE -ne 0){throw 'SQL failed; preserve partial state and stop'}
    return $result
}
$identity=Query 'SELECT VERSION(),@@port,@@datadir,DATABASE();'
$parts=$identity -split "`t"
if($parts.Count -ne 4 -or $parts[0] -ne '5.7.24' -or [int]$parts[1] -ne $Port -or $parts[3] -ne 'electronic_register_test' -or [IO.Path]::GetFullPath($parts[2]).TrimEnd('\','/') -ne $data){throw 'MySQL instance identity rejected'}
$column=Query "SELECT COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_role' AND COLUMN_NAME='role';"
if($column -cne "enum('InstitutionAdmin','Teacher','Student')"){throw 'Unexpected source ENUM; never replay or overwrite'}
if((Query 'SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE();') -ne '43' -or
   (Query 'SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE();') -ne '98' -or
   (Query 'SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE();') -ne '131') {throw 'Unexpected source schema inventory'}
$baselineText=(& git -C $root show d5bf774d57eb143252a3659a29e653e4a0ce19a9:migrations/bootstrap/schema-contract.json) -join "`n"
if($LASTEXITCODE -ne 0){throw 'Source schema contract unavailable'}
$baseline=$baselineText | ConvertFrom-Json
foreach($name in @('mt5c_invitation_insert','mt5d_event_insert','mt5d_event_snapshot_shape','mt3a_audit_insert','mt3a_audit_update')){
    $definition=$baseline.trigger_metadata.$name
    $statement=$definition.ACTION_STATEMENT.Replace("'","''")
    if((Query "SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME='$name' AND BINARY ACTION_STATEMENT=BINARY '$statement' AND SQL_MODE='$($definition.SQL_MODE)' AND CHARACTER_SET_CLIENT='$($definition.CHARACTER_SET_CLIENT)' AND COLLATION_CONNECTION='$($definition.COLLATION_CONNECTION)';") -ne '1'){throw "Unexpected source trigger $name"}
}
if((Query 'SELECT COUNT(*) FROM information_schema.INNODB_TRX;') -ne '0' -or (Query 'SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();') -ne '0'){throw 'Stop all writers and connections before DDL'}
New-Item -ItemType Directory -Path $evidence | Out-Null
$identity | Set-Content (Join-Path $evidence 'identity.tsv')
$rolesBefore=@(Query 'SELECT membership_id,role,role+0 FROM institution_membership_role ORDER BY membership_id,role;')
[IO.File]::WriteAllLines((Join-Path $evidence 'roles-before.tsv'), [string[]]$rolesBefore)
Query 'SELECT COUNT(*) FROM institution_membership_event;' | Set-Content (Join-Path $evidence 'audit-count-before.txt')
& (Join-Path $MySqlBin 'mysqldump.exe') --no-defaults --protocol=TCP --host=127.0.0.1 "--port=$Port" --user=root --databases electronic_register_test --single-transaction --routines --triggers "--result-file=$evidence/backup.sql"
if($LASTEXITCODE -ne 0){throw 'Backup failed; no DDL performed'}
Get-FileHash (Join-Path $evidence 'backup.sql') -Algorithm SHA256 | Format-List | Out-File (Join-Path $evidence 'backup-sha256.txt')
if(!$Apply){Write-Output 'Preflight and backup complete; DDL not requested';return}
'20261027.1 DDL implicitly commits; failure requires manual recovery' | Set-Content (Join-Path $evidence 'ddl-started.txt')
Query (Get-Content -Raw (Join-Path $root 'migrations/institution-staff/001_expand.sql')) | Out-Null
$results=Query (Get-Content -Raw (Join-Path $root 'migrations/institution-staff/002_validate.sql'))
$results | Set-Content (Join-Path $evidence 'validation.tsv')
foreach($row in $results){if(($row -split "`t")[-1] -ne '0'){throw 'Postflight failed; preserve state'}}
$before=Get-Content (Join-Path $evidence 'roles-before.tsv')
$after=Query 'SELECT membership_id,role,role+0 FROM institution_membership_role ORDER BY membership_id,role;'
if(($before -join "`n") -cne ($after -join "`n")){throw 'Existing roles/SQL ordinals changed'}
if((Query 'SELECT COUNT(*) FROM institution_membership_event;') -ne (Get-Content (Join-Path $evidence 'audit-count-before.txt'))){throw 'Existing IAM audit changed'}
'20261027.1' | Set-Content (Join-Path $evidence 'ddl-completed.txt')
Write-Output 'Staff migration preflight, backup and postflight passed'
