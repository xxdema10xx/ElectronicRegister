[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ContainerName,
    [Parameter(Mandatory=$true)][int]$Port,
    [Parameter(Mandatory=$true)][string]$BindingConfig,
    [Parameter(Mandatory=$true)][switch]$WritersStopped
)
# Applies the er-platform-identity-1 successor ONCE to a NEW disposable Docker
# mysql:8.4.13 container that already carries er-quality-2-mysql84. Never targets
# 3306/3307/13306, mysql84-local or a container without the disposable label.
# DDL implicitly commits: on any failure preserve the container and evidence; no replay/restore.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$WritersStopped -or $Port -in @(3306,3307,13306) -or $Port -lt 1024 -or $Port -gt 65535 -or $ContainerName -eq 'mysql84-local' -or $ContainerName -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,62}$'){throw 'Only a new disposable local 8.4 container with stopped writers is supported'}
if(!$env:MYSQL_PWD){throw 'MYSQL_PWD must contain the disposable root credential; never pass it on the command line'}
$inspected=docker inspect $ContainerName
if($LASTEXITCODE -ne 0){throw 'Container not found; no DDL executed'}
$info=@(($inspected -join "`n")|ConvertFrom-Json)
if($info.Count -ne 1 -or $info[0].Config.Labels.'er.platform-identity.disposable' -cne '1' -or $info[0].Config.Image -cne 'mysql:8.4.13' -or $info[0].State.Running -ne $true){throw 'Container is not a running disposable mysql:8.4.13 created for this runner; no DDL executed'}
$mapped=docker port $ContainerName 3306/tcp
if($LASTEXITCODE -ne 0 -or @($mapped|Where-Object {$_ -eq "127.0.0.1:$Port"}).Count -ne 1){throw 'Container port binding differs from the attested loopback port; no DDL executed'}
$binding=Get-Content -Raw $BindingConfig|ConvertFrom-Json
if($binding.database -cne 'electronic_register_test' -or $binding.ownedFixture -or $binding.successor){throw 'Binding must target electronic_register_test without fixture options'}
$contract=Get-Content -Raw (Join-Path $root 'migrations/platform-identity/predecessor-contract.json')|ConvertFrom-Json
function Query([string]$sql){$result=$sql|docker exec -i -e MYSQL_PWD $ContainerName mysql --user=root --default-character-set=utf8mb4 --batch --raw --skip-column-names electronic_register_test;if($LASTEXITCODE -ne 0){throw 'SQL failed; preserve evidence and do not replay'};return $result}
function Inventory(){
    return [ordered]@{
        tables=[int](Query 'SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE();')
        triggers=[int](Query 'SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE();')
        fks=[int](Query 'SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE();')
        indexes=[int](Query 'SELECT COUNT(*) FROM (SELECT DISTINCT TABLE_NAME,INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE()) x;')
    }
}
function RequireInventory($expected){$actual=Inventory;foreach($k in 'tables','triggers','fks','indexes'){if($actual[$k] -ne [int]$expected.$k){throw "Inventory differs ($k); no replay permitted"}}}
function Columns(){return @(Query "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='user' ORDER BY ORDINAL_POSITION;")}
function Markers(){return @(Query 'SELECT version FROM er_schema_release ORDER BY version;')}
$identity=(Query 'SELECT VERSION(),@@port,@@server_uuid,@@hostname;') -split "`t"
if($identity[0] -ne '8.4.13' -or $identity[1] -ne '3306'){throw 'Attested 8.4.13 container identity mismatch; no DDL executed'}
if((Query "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID() AND USER NOT IN ('event_scheduler','system user');") -ne '0'){throw 'Writers/connections still present; no DDL executed'}
# Predecessor: exact markers, inventory and User columns; successor objects absent.
if((Markers) -join ',' -cne ($contract.predecessor.markers -join ',')){throw 'Predecessor release markers differ; no DDL executed'}
RequireInventory $contract.predecessor.inventory
if((Columns) -join ',' -cne ($contract.predecessor.userColumns -join ',')){throw 'Predecessor User columns differ; no DDL executed'}
$evidence=Join-Path $root ('.staff-private/platform-identity-migration-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $evidence|Out-Null
$identity -join "`t"|Set-Content (Join-Path $evidence 'identity.tsv')
docker exec -e MYSQL_PWD $ContainerName mysqldump --user=root --single-transaction --routines --triggers --events electronic_register_test|Set-Content -Encoding utf8 (Join-Path $evidence 'before.sql')
if($LASTEXITCODE -ne 0){throw 'Backup failed; no DDL executed'}
Get-FileHash (Join-Path $evidence 'before.sql') -Algorithm SHA256|Select-Object Hash|ConvertTo-Json|Set-Content (Join-Path $evidence 'backup-sha256.json')
$bound=Join-Path $evidence 'successor'
& node (Join-Path $root 'tools/mysql84-release.cjs') successor $BindingConfig $bound
if($LASTEXITCODE -ne 0){throw 'Successor binding failed; no DDL executed'}
# DDL implicitly commits. Never catch and automatically retry/restore a partial application.
Query ([IO.File]::ReadAllText((Join-Path $bound '01-platform-identity.sql')))|Out-Null
if((Markers) -join ',' -cne ($contract.target.markers -join ',')){throw 'Target markers differ; preserve the container and backup'}
RequireInventory $contract.target.inventory
if((Columns) -join ',' -cne ($contract.target.userColumns -join ',')){throw 'Target User columns differ; preserve the container and backup'}
foreach($p in $contract.target.tables.PSObject.Properties){
    $actual=(((Query "SHOW CREATE TABLE ``$($p.Name)``;") -join "`n") -split "`t",2)[1]
    if($actual -cne $p.Value){throw "Target table definition differs: $($p.Name)"}
}
foreach($p in $contract.target.triggers.PSObject.Properties){
    $t=$p.Value
    $row=(Query "SELECT EVENT_OBJECT_TABLE,EVENT_MANIPULATION,ACTION_TIMING,SQL_MODE,UPPER(SHA2(REPLACE(ACTION_STATEMENT,CHAR(13),''),256)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME='$($p.Name)';") -split "`t"
    if($row.Count -ne 5 -or $row[0] -cne $t.table -or $row[1] -cne $t.event -or $row[2] -cne $t.timing -or $row[3] -cne $t.mode -or $row[4] -cne $t.unboundStatementSha256){throw "Target trigger differs: $($p.Name)"}
}
$checks=@(Query ([IO.File]::ReadAllText((Join-Path $root 'migrations/platform-identity/002_validate.sql'))))
$checks|Set-Content (Join-Path $evidence 'postflight.tsv')
if($checks.Count -ne [int]$contract.target.validation.checks -or @($checks|Where-Object {($_ -split "`t")[-1] -ne '0'}).Count -ne 0){throw 'Postflight failed; preserve the container and backup'}
Write-Output "er-platform-identity-1 verified on the disposable container; evidence: $evidence"
