[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MySqlBin,
    [Parameter(Mandatory=$true)][int]$Port,
    [switch]$Baseline
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if($Port -lt 1024 -or $Port -gt 65535 -or $Port -in @(3306,13306)){throw 'A new isolated test port is required'}
if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'Port occupied; never stop or reuse that instance'}
$server=Join-Path $MySqlBin 'mysqld.exe'
if((& $server --no-defaults --version) -notmatch '\b5\.7\.24\b'){throw 'MySQL 5.7.24 required'}
$run=Join-Path $root ('.staff-private/instance-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl=New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
foreach($id in @($sid.Value,'S-1-5-18','S-1-5-32-544')){
    $identity=New-Object Security.Principal.SecurityIdentifier($id)
    $rule=New-Object Security.AccessControl.FileSystemAccessRule($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
    $acl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $run -AclObject $acl
$data=Join-Path $run 'data'
$init=Start-Process $server -ArgumentList @('--no-defaults','--initialize-insecure',"--datadir=$data", "--basedir=$((Split-Path $MySqlBin -Parent))") -WindowStyle Hidden -RedirectStandardOutput "$run/init-out.log" -RedirectStandardError "$run/init-error.log" -PassThru -Wait
if($init.ExitCode -ne 0){throw 'Initialization failed; preserve datadir and logs'}
$process=Start-Process $server -ArgumentList @('--no-defaults',"--datadir=$data", "--basedir=$((Split-Path $MySqlBin -Parent))", "--port=$Port",'--bind-address=127.0.0.1','--max-connections=200','--explicit-defaults-for-timestamp=1',"--log-error=$run/mysql-error.log") -WindowStyle Hidden -RedirectStandardOutput "$run/server-out.log" -RedirectStandardError "$run/server-error.log" -PassThru
[IO.File]::WriteAllText("$run/pid.txt",[string]$process.Id)
$client=Join-Path $MySqlBin 'mysql.exe'
$arguments=@('--no-defaults','--protocol=TCP','--host=127.0.0.1',"--port=$Port",'--user=root','--batch','--raw','--skip-column-names','--default-character-set=utf8mb4')
$previous=$env:MYSQL_PWD
try {
    Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    $ready=$false
    for($i=0;$i -lt 80;$i++){
        if($process.HasExited){throw 'Server exited; preserve logs'}
        'SELECT 1;' | & $client @arguments 2>$null | Out-Null
        if($LASTEXITCODE -eq 0){$ready=$true;break}
        Start-Sleep -Milliseconds 200
    }
    if(!$ready){throw 'Readiness failed; no restart/replay'}
    $password=[guid]::NewGuid().ToString('N')+[guid]::NewGuid().ToString('N')
    "ALTER USER 'root'@'localhost' IDENTIFIED BY '$password'; CREATE DATABASE electronic_register_test CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE USER 'er_test'@'localhost'; GRANT ALL PRIVILEGES ON electronic_register_test.* TO 'er_test'@'localhost';" | & $client @arguments
    if($LASTEXITCODE -ne 0){throw 'Private instance provisioning failed'}
    [IO.File]::WriteAllText("$run/password.txt",$password)
    $env:MYSQL_PWD=$password
    if($Baseline){$sql=(& git -C $root show d5bf774d57eb143252a3659a29e653e4a0ce19a9:migrations/bootstrap/001_schema.sql) -join "`n";if($LASTEXITCODE -ne 0){throw 'Baseline source unavailable'}}
    else {$sql=[IO.File]::ReadAllText((Join-Path $root 'migrations/bootstrap/001_schema.sql'))}
    $sql | & $client @arguments --database=electronic_register_test
    if($LASTEXITCODE -ne 0){throw 'Bootstrap failed; never reinitialize/replay'}
    "INSERT INTO institution(id,name,code,status) VALUES ('4d543341-0000-4000-8000-000000000001','Technical legacy test holding','MT3A_LEGACY_TEST','provisioning'); INSERT INTO mt3a_legacy_scope(scope,institution_id) VALUES ('legacy','4d543341-0000-4000-8000-000000000001');" | & $client @arguments --database=electronic_register_test
    if($LASTEXITCODE -ne 0){throw 'Technical test holding setup failed'}
    "SELECT VERSION(),@@port,@@datadir;" | & $client @arguments | Set-Content "$run/identity.tsv"
    [IO.File]::WriteAllText((Join-Path $root '.staff-private/latest-instance.txt'),$run)
    Write-Output "Isolated instance ready: $run (port $Port; PID $($process.Id))"
} finally {$env:MYSQL_PWD=$previous}
