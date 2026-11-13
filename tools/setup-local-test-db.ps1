[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MySqlBin,
    [int]$Port=13306,
    [string]$DataDir,
    [string]$MainDataDir='C:\MAMP\db\mysql',
    [switch]$CreateInstance,
    [switch]$VerifyOnly,
    [switch]$ConfirmIsolatedTest,
    [Security.SecureString]$Password
)
$ErrorActionPreference='Stop'
if(!$Password){$Password=Read-Host 'Private MySQL root password (never stored)' -AsSecureString}
$previous=$env:ER_TEST_DB_PASSWORD
$ptr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try {
    $env:ER_TEST_DB_PASSWORD=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    $arguments=@((Join-Path $PSScriptRoot 'local_test_db.py'),'--mysql-bin',$MySqlBin,'--port',[string]$Port,'--main-datadir',$MainDataDir)
    if($DataDir){$arguments+=@('--datadir',$DataDir)}
    if($CreateInstance){$arguments+='--create-instance'}
    if($VerifyOnly){$arguments+='--verify-only'}
    if($ConfirmIsolatedTest){$arguments+='--confirm-isolated-test'}
    & python @arguments
    if($LASTEXITCODE -ne 0){throw 'Test database setup failed; preserve state and private journal. No automatic retry.'}
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    $env:ER_TEST_DB_PASSWORD=$previous
}
