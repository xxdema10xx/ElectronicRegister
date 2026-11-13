[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MySqlBin,
    [Parameter(Mandatory=$true)][string]$PrivateEvidenceDirectory,
    [Parameter(Mandatory=$true)][string]$ExpectedDataDirectory,
    [int]$Port=13306,
    [Security.SecureString]$Password,
    [switch]$Apply
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if($Port -eq 3306 -or $Port -lt 1024 -or $Port -gt 65535){throw 'Forbidden or invalid test port'}
$expectedData=[IO.Path]::GetFullPath($ExpectedDataDirectory).Replace('\','/').TrimEnd('/')+'/'
$privateRoot=([IO.Path]::GetFullPath((Join-Path $root '.exam-private')).Replace('\','/').TrimEnd('/')+'/')
if(!$expectedData.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Only a newly provisioned private workspace test datadir is allowed'}
$evidence=[IO.Path]::GetFullPath($PrivateEvidenceDirectory)
if(Test-Path -LiteralPath $evidence){throw 'Evidence directory already exists; no automatic retry'}
if(!$evidence.StartsWith((Join-Path $root '.exam-private')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be under this workspace ignored .exam-private directory'}
$client=Join-Path $MySqlBin 'mysql.exe'
$dump=Join-Path $MySqlBin 'mysqldump.exe'
$argsDb=@('--no-defaults','--protocol=TCP','--host=127.0.0.1',"--port=$Port",'--user=root','--database=electronic_register_test','--batch','--skip-column-names')
$previousPassword=$env:MYSQL_PWD
$ptr=[IntPtr]::Zero
try {
    if($Password){$ptr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password);$env:MYSQL_PWD=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)}
    function Query([string]$sql) {
        $result=$sql | & $client @argsDb
        if($LASTEXITCODE -ne 0){throw 'SQL failed; preserve state and stop. No automatic retry.'}
        return $result
    }
    $identity=Query "SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE();"
    $parts=$identity -split "`t"
    if($parts.Count -ne 4 -or $parts[0] -ne '5.7.24' -or $parts[1] -ne "$Port" -or $parts[2] -ne $expectedData -or $parts[3] -ne 'electronic_register_test'){throw 'Isolated MySQL identity rejected'}
    if((Query "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_administration_event';") -ne '0'){throw 'Migration already applied or bootstrap already current; never replay'}
    if((Query "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('study_area','study_path','biennium','biennium_study_area','biennium_study_path','class_subject','campus') AND COLUMN_NAME='institution_id' AND IS_NULLABLE='NO';") -ne '7'){throw 'Institutional parent schema preflight failed'}
    if((Query 'SELECT COUNT(*) FROM information_schema.INNODB_TRX;') -ne '0'){throw 'Active transactions; stop writers first'}
    if((Query 'SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();') -ne '0'){throw 'Other connections; stop writers first'}
    New-Item -ItemType Directory -Path $evidence | Out-Null
    $identity | Set-Content (Join-Path $evidence 'identity.txt')
    Query "SELECT TABLE_NAME,CONSTRAINT_NAME,COLUMN_NAME,REFERENCED_TABLE_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE REFERENCED_TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME IN ('study_area','study_path','biennium','biennium_study_area','biennium_study_path','class_subject','campus') ORDER BY REFERENCED_TABLE_NAME,TABLE_NAME,CONSTRAINT_NAME,ORDINAL_POSITION;" | Set-Content (Join-Path $evidence 'incoming-resource-fks.tsv')
    & $dump '--no-defaults' '--protocol=TCP' '--host=127.0.0.1' "--port=$Port" '--user=root' '--databases' 'electronic_register_test' '--single-transaction' '--routines' '--triggers' "--result-file=$evidence/pre-migration.sql"
    if($LASTEXITCODE -ne 0){throw 'Backup failed; no DDL performed'}
    Get-FileHash (Join-Path $evidence 'pre-migration.sql') -Algorithm SHA256 | Format-List | Out-File (Join-Path $evidence 'backup-sha256.txt')
    if(!$Apply){Write-Output 'Preflight and backup completed. No DDL requested.';return}
    '20261022.3' | Set-Content (Join-Path $evidence 'ddl-started.txt')
    Query (Get-Content -Raw (Join-Path $root 'migrations/institution-administration/001_expand.sql')) | Out-Null
    $columns=Query "SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN ('administration_event_insert','administration_event_update','administration_event_delete');"
    if($columns -ne '3'){throw 'DDL postflight failed; preserve partial state'}
    $validation=@(Query (Get-Content -Raw (Join-Path $root 'migrations/institution-administration/002_validate.sql')))
    $validation | Set-Content (Join-Path $evidence 'validation.tsv')
    if($validation.Count -ne 4 -or @($validation | Where-Object { ($_ -split "`t")[-1] -ne '0' }).Count){throw 'Administration audit validator failed; preserve state'}
    '20261022.3' | Set-Content (Join-Path $evidence 'ddl-completed.txt')
} finally {
    $env:MYSQL_PWD=$previousPassword
    if($ptr -ne [IntPtr]::Zero){[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)}
}
