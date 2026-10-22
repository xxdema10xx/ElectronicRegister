[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MySqlBin,
    [Security.SecureString]$Password,
    [string]$Filter
)
$ErrorActionPreference='Stop'
if(!$Password){$Password=Read-Host 'Private test root password' -AsSecureString}
& (Join-Path $PSScriptRoot 'setup-local-test-db.ps1') -MySqlBin $MySqlBin -Password $Password -VerifyOnly
$repo=Split-Path $PSScriptRoot -Parent
$results=Join-Path $repo ('migrations/mt4/private/test-runs/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
$env:ER_TEST_RESULTS_DIRECTORY=$results
try {
    & python -c "import os,sys; sys.path.insert(0,sys.argv[1]); from pathlib import Path; from local_test_db import private_acl; private_acl(Path(os.environ['ER_TEST_RESULTS_DIRECTORY']))" $PSScriptRoot
    if($LASTEXITCODE -ne 0){throw 'Private result ACL verification failed'}
} finally {Remove-Item Env:ER_TEST_RESULTS_DIRECTORY -ErrorAction SilentlyContinue}
$previous=@{}
foreach($name in @('TEST_MYSQL_CONNECTION','ER_TEST_STRICT_SESSION','MYSQLDUMP_EXE','MYSQLDUMP_FLAVOR','ER_MT5B_SQL_PROOF')){$previous[$name]=[Environment]::GetEnvironmentVariable($name)}
$ptr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try {
    $builder=New-Object System.Data.Common.DbConnectionStringBuilder
    $builder['Server']='127.0.0.1';$builder['Port']=13306;$builder['Database']='electronic_register_test';$builder['User ID']='root'
    $builder['Password']=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    $builder['Allow User Variables']=$true;$builder['SslMode']='None'
    $env:TEST_MYSQL_CONNECTION=$builder.ConnectionString
    $env:ER_TEST_STRICT_SESSION='1';$env:MYSQLDUMP_EXE=Join-Path $MySqlBin 'mysqldump.exe';$env:MYSQLDUMP_FLAVOR='MySQL57'
    Remove-Item Env:ER_MT5B_SQL_PROOF -ErrorAction SilentlyContinue
    & dotnet build (Join-Path $repo 'Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx') --verbosity minimal
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    foreach($project in @('Unit','Integration')){
        $arguments=@('test',(Join-Path $repo "tests/$project/$project.Tests.csproj"),'--no-build','--logger','trx;LogFileName=result.trx','--results-directory',(Join-Path $results $project),'--verbosity','minimal')
        if($Filter){$arguments+=@('--filter',$Filter)}
        & dotnet @arguments
        if($LASTEXITCODE -ne 0){throw "$project tests failed"}
        [xml]$trx=Get-Content -Raw -LiteralPath (Join-Path $results "$project/result.trx")
        $c=$trx.TestRun.ResultSummary.Counters
        if([int]$c.total -eq 0 -or $c.total -ne $c.passed -or $c.total -ne $c.executed){throw 'Tests missing, failed, skipped or not executed'}
    }
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name])}
}
& (Join-Path $PSScriptRoot 'setup-local-test-db.ps1') -MySqlBin $MySqlBin -Password $Password -VerifyOnly
Write-Output "Results retained privately at $results"
