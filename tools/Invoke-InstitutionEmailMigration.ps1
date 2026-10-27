[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MySqlBin,
    [Parameter(Mandatory=$true)][int]$Port,
    [Parameter(Mandatory=$true)][string]$ExpectedDataDirectory,
    [Parameter(Mandatory=$true)][switch]$WritersStopped
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$data=[IO.Path]::GetFullPath($ExpectedDataDirectory).TrimEnd('\','/').Replace('\','/')+'/'
$prefix=[IO.Path]::GetFullPath((Join-Path $root '.staff-private')).Replace('\','/')+'/'
if(!$WritersStopped -or !$data.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or $Port -in @(3306,13306) -or $Port -lt 1024 -or $Port -gt 65535){throw 'Only a new isolated local test target with stopped writers is supported'}
if(!$env:MYSQL_PWD){throw 'MYSQL_PWD must contain the isolated root credential; never pass it on the command line'}
$client=Join-Path $MySqlBin 'mysql.exe'
$arguments=@('--no-defaults','--protocol=TCP','--host=127.0.0.1',"--port=$Port",'--user=root','--database=electronic_register_test','--default-character-set=utf8mb4')
function Query([string]$sql){$result=$sql | & $client @arguments --batch --raw --skip-column-names;if($LASTEXITCODE -ne 0){throw 'SQL failed; preserve evidence and do not replay'};return $result}
function XmlQuery([string]$sql){$result=$sql | & $client @arguments --xml;if($LASTEXITCODE -ne 0){throw 'Schema read failed'};return [xml]($result -join "`n")}
$identity=(Query "SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/'),@@server_uuid;") -split "`t"
if($identity[0] -ne '5.7.24' -or [int]$identity[1] -ne $Port -or $identity[2] -ine $data){throw 'Attested instance identity mismatch; no DDL executed'}
if((Query 'SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();') -ne '0'){throw 'Writers/connections still present; no DDL executed'}
$before=Get-Content -Raw (Join-Path $root 'migrations/institution-email/predecessor-contract.json')|ConvertFrom-Json
$after=Get-Content -Raw (Join-Path $root 'migrations/bootstrap/schema-contract.json')|ConvertFrom-Json
function VerifyContract($contract){
    $names=@((XmlQuery 'SHOW FULL TABLES;').resultset.row)
    if($names.Count -ne $contract.inventory.tables){throw 'Table inventory differs; no replay permitted'}
    foreach($p in $contract.tables.PSObject.Properties){
        $result=XmlQuery "SHOW CREATE TABLE ``$($p.Name)``;"
        $actual=($result.resultset.row.field|Where-Object {$_.name -eq 'Create Table'}).InnerText
        if($actual -cne $p.Value){throw "Table definition differs: $($p.Name)"}
    }
    $triggers=XmlQuery 'SELECT TRIGGER_NAME,ACTION_STATEMENT,ACTION_ORDER,SQL_MODE,DEFINER,CHARACTER_SET_CLIENT,COLLATION_CONNECTION,DATABASE_COLLATION FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE();'
    if(@($triggers.resultset.row).Count -ne $contract.inventory.triggers){throw 'Trigger inventory differs'}
    foreach($row in $triggers.resultset.row){
        $values=@{};foreach($field in $row.field){$values[$field.name]=$field.InnerText}
        $expected=$contract.trigger_metadata.($values.TRIGGER_NAME)
        if(!$expected){throw 'Unclassified trigger'}
        foreach($key in $values.Keys){if([string]$expected.$key -cne [string]$values[$key]){throw "Trigger metadata differs: $($values.TRIGGER_NAME), $key"}}
    }
}
VerifyContract $before
$evidence=Join-Path $root ('.staff-private/email-migration-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $evidence|Out-Null
$identity -join "`t"|Set-Content (Join-Path $evidence 'identity.tsv')
& (Join-Path $MySqlBin 'mysqldump.exe') --no-defaults --protocol=TCP --host=127.0.0.1 --port=$Port --user=root --single-transaction --routines --triggers --events --result-file="$evidence/before.sql" electronic_register_test
if($LASTEXITCODE -ne 0){throw 'Backup failed; no DDL executed'}
Get-FileHash "$evidence/before.sql" -Algorithm SHA256|Select-Object Hash|ConvertTo-Json|Set-Content "$evidence/backup-sha256.json"
# DDL implicitly commits. Never catch and automatically retry/restore a partial application.
Query ([IO.File]::ReadAllText((Join-Path $root 'migrations/institution-email/001_expand.sql')))|Out-Null
VerifyContract $after
$checks=@(Query ([IO.File]::ReadAllText((Join-Path $root 'migrations/institution-email/002_validate.sql'))))
$checks|Set-Content "$evidence/postflight.tsv"
if($checks.Count -ne 10 -or @($checks|Where-Object {($_ -split "`t")[-1] -ne '0'}).Count -ne 0){throw 'Postflight failed; preserve the instance and backup'}
Write-Output "Institutional email additive migration verified on the attested isolated instance; evidence: $evidence"
