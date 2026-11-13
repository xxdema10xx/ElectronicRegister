[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$MySqlBin,[Parameter(Mandatory=$true)][int]$Port,[Parameter(Mandatory=$true)][string]$ExpectedDataDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$data=[IO.Path]::GetFullPath($ExpectedDataDirectory).TrimEnd('\','/').Replace('\','/')+'/'
$prefix=[IO.Path]::GetFullPath((Join-Path $root '.staff-private')).Replace('\','/')+'/'
if(!$data.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or $Port -in @(3306,13306) -or $Port -lt 1024 -or $Port -gt 65535){throw 'Disposable target required'}
$client=Join-Path $MySqlBin 'mysql.exe'
$arguments=@('--no-defaults','--protocol=TCP','--host=127.0.0.1',"--port=$Port",'--user=root','--database=electronic_register_test','--batch','--raw','--skip-column-names')
function Query([string]$sql){$result=$sql | & $client @arguments;if($LASTEXITCODE -ne 0){throw 'SQL failed; stop'};return $result}
$identity=(Query "SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/');") -split "`t"
if($identity[0] -ne '5.7.24' -or [int]$identity[1] -ne $Port -or $identity[2] -ine $data){throw 'Disposable identity mismatch'}
if((Query 'SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();') -ne '0'){throw 'Stop test writers first'}
$contract=Get-Content -Raw (Join-Path $root 'migrations/bootstrap/schema-contract.json') | ConvertFrom-Json
# These are existing root-only fixture teardown guards. Bind the same predicates
# to ONE new instance; production/bootstrap sources and all business guards stay intact.
foreach($property in $contract.triggers.PSObject.Properties){
    $trigger=$property.Value
    if(!$trigger.statement.Contains('@@port=13306')){continue}
    $old=$contract.trigger_metadata.($property.Name).ACTION_STATEMENT
    $escaped=$old.Replace("'","''")
    if((Query "SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME='$($property.Name)' AND BINARY ACTION_STATEMENT=BINARY '$escaped';") -ne '1'){throw 'Unexpected retention definition; never overwrite'}
    $replacement=$trigger.statement.Replace('@@port=13306',"@@port=$Port").Replace("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'", "LOWER(REPLACE(@@datadir,CHAR(92),'/'))='$(($data.ToLowerInvariant()).Replace("'","''"))'")
    $sql="SET SESSION sql_mode='$($trigger.mode)';`nSET NAMES $($trigger.charset) COLLATE $($trigger.collation);`nDROP TRIGGER ``$($property.Name)``;`nDELIMITER `$`$`n$replacement`$`$`nDELIMITER ;"
    Query $sql | Out-Null
}
Write-Output 'Fixture retention guards bound to the exact new test instance; no production source changed'
