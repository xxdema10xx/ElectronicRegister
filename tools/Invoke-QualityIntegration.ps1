param(
    [Parameter(Mandatory=$true)][string]$InstanceDirectory,
    [Parameter(Mandatory=$true)][ValidateRange(1024,65535)][int]$Port,
    [Parameter(Mandatory=$true)][string]$MySqlBin,
    [string]$Filter,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name = 'targeted'
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot/..").Path
$run=[IO.Path]::GetFullPath($InstanceDirectory).TrimEnd('\','/')
if(!$run.StartsWith((Join-Path $repo '.staff-private')+'\',[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $run -Leaf) -notmatch '^instance-[0-9a-f]{32}$' -or $Port -in @(3306,13306)){throw 'Explicit disposable instance inside workspace required'}
if(Test-Path -LiteralPath "$run/$Name"){throw 'Evidence directory already exists; choose a new run name'}
$previous=@{}
foreach($key in @('TEST_MYSQL_CONNECTION','ER_TEST_ISOLATED_PORT','ER_TEST_ISOLATED_DATADIR','ER_TEST_STRICT_SESSION','MYSQLDUMP_EXE','MYSQLDUMP_FLAVOR','ER_MT5B_SQL_PROOF')){$previous[$key]=[Environment]::GetEnvironmentVariable($key)}
try {
    $connection=New-Object System.Data.Common.DbConnectionStringBuilder
    $connection['Server']='127.0.0.1'; $connection['Port']=$Port; $connection['Database']='electronic_register_test'; $connection['User ID']='root'
    $connection['Password']=[IO.File]::ReadAllText("$run/password.txt"); $connection['Allow User Variables']=$true; $connection['SslMode']='None'
    $env:TEST_MYSQL_CONNECTION=$connection.ConnectionString
    $env:ER_TEST_ISOLATED_PORT=[string]$Port; $env:ER_TEST_ISOLATED_DATADIR="$run/data"; $env:ER_TEST_STRICT_SESSION='1'
    $env:MYSQLDUMP_EXE=Join-Path $MySqlBin 'mysqldump.exe'; $env:MYSQLDUMP_FLAVOR='MySQL57'
    Remove-Item Env:ER_MT5B_SQL_PROOF -ErrorAction SilentlyContinue
    $arguments=@('test',"$repo/tests/Integration/Integration.Tests.csproj",'--no-build','--no-restore','--logger','trx;LogFileName=result.trx','--results-directory',"$run/$Name",'-v','minimal')
    if($Filter){$arguments+=@('--filter',$Filter)}
    $ErrorActionPreference='Continue'
    & dotnet @arguments *> "$run/$Name.log"
    $testExit=$LASTEXITCODE
    $ErrorActionPreference='Stop'
    Get-Content "$run/$Name.log" -Tail 30
    if($testExit -ne 0){throw "Tests failed; private results at $run/$Name"}
} finally {foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key])}}
