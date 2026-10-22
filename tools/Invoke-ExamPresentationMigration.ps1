[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MySqlBin,
    [Parameter(Mandatory=$true)][string]$PrivateEvidenceDirectory,
    [Security.SecureString]$Password,
    [switch]$Apply
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$evidence=[IO.Path]::GetFullPath($PrivateEvidenceDirectory)
if(Test-Path -LiteralPath $evidence){throw 'Evidence directory already exists; no automatic retry'}
if(!$evidence.StartsWith((Join-Path $root '.exam-private')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be under this workspace ignored .exam-private directory'}
$client=Join-Path $MySqlBin 'mysql.exe'
$dump=Join-Path $MySqlBin 'mysqldump.exe'
$argsDb=@('--no-defaults','--protocol=TCP','--host=127.0.0.1','--port=13306','--user=root','--database=electronic_register_test','--batch','--skip-column-names')
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
    if($identity -notmatch '^5\.7\.24\s+13306\s+.*?/migrations/mt4/private/mt5a-mamp-13306/data/\s+electronic_register_test$'){throw 'Isolated MySQL identity rejected'}
    if((Query "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='exam_submission' AND COLUMN_NAME='display_order_json';") -ne '0'){throw 'Migration already applied or bootstrap already current; never replay'}
    if((Query 'SELECT COUNT(*) FROM information_schema.INNODB_TRX;') -ne '0'){throw 'Active transactions; stop writers first'}
    if((Query 'SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();') -ne '0'){throw 'Other connections; stop writers first'}
    New-Item -ItemType Directory -Path $evidence | Out-Null
    $identity | Set-Content (Join-Path $evidence 'identity.txt')
    Query 'SELECT e.institution_id,e.id,e.name AS historical_name,s.name AS subject_name FROM exam e JOIN class_subject cs ON cs.id=e.class_subject_id AND cs.institution_id=e.institution_id JOIN subject s ON s.id=cs.subject_id AND s.institution_id=cs.institution_id WHERE BINARY e.name<>BINARY s.name;' | Set-Content (Join-Path $evidence 'name-reconciliation-inventory.tsv')
    & $dump '--no-defaults' '--protocol=TCP' '--host=127.0.0.1' '--port=13306' '--user=root' '--databases' 'electronic_register_test' '--single-transaction' '--routines' '--triggers' "--result-file=$evidence/pre-migration.sql"
    if($LASTEXITCODE -ne 0){throw 'Backup failed; no DDL performed'}
    Get-FileHash (Join-Path $evidence 'pre-migration.sql') -Algorithm SHA256 | Format-List | Out-File (Join-Path $evidence 'backup-sha256.txt')
    if(!$Apply){Write-Output 'Preflight and backup completed. No DDL requested.';return}
    '20261022.1' | Set-Content (Join-Path $evidence 'ddl-started.txt')
    Query (Get-Content -Raw (Join-Path $root 'migrations/exam-presentation/001_expand.sql')) | Out-Null
    $columns=Query "SELECT CONCAT(DATA_TYPE,':',IS_NULLABLE) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='exam_submission' AND COLUMN_NAME='display_order_json';"
    if($columns -ne 'longtext:YES'){throw 'DDL postflight failed; preserve partial state'}
    Query (Get-Content -Raw (Join-Path $root 'migrations/exam-presentation/002_validate.sql')) | Set-Content (Join-Path $evidence 'validation.tsv')
    '20261022.1' | Set-Content (Join-Path $evidence 'ddl-completed.txt')
} finally {
    $env:MYSQL_PWD=$previousPassword
    if($ptr -ne [IntPtr]::Zero){[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)}
}
