param(
 [ValidateSet('Validate','Apply')][string]$Mode='Validate',
 [Parameter(Mandatory=$true)][string]$ClientConfig,
 [string]$BackupPath,
 [string]$Mysql='C:/MAMP/bin/mysql/bin/mysql.exe',
 [string]$Mysqldump='C:/Program Files/MySQL/MySQL Server 8.0/bin/mysqldump.exe'
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$ClientConfig=[IO.Path]::GetFullPath($ClientConfig)
if(!(Test-Path -LiteralPath $ClientConfig)-or $ClientConfig.Contains('"')){throw 'Private client config required'}
function Invoke-Client([string]$sql){
 $si=[Diagnostics.ProcessStartInfo]::new($Mysql);$si.UseShellExecute=$false;$si.CreateNoWindow=$true
 $si.RedirectStandardInput=$true;$si.RedirectStandardOutput=$true;$si.RedirectStandardError=$true
 $si.Arguments="--defaults-file=`"$ClientConfig`" --protocol=TCP --host=127.0.0.1 --port=13306 --database=electronic_register_test --batch --raw --skip-column-names --default-character-set=utf8mb4"
 $si.EnvironmentVariables.Remove('MYSQL_PWD')
 $p=[Diagnostics.Process]::Start($si);$output=$p.StandardOutput.ReadToEndAsync();$errors=$p.StandardError.ReadToEndAsync()
 $p.StandardInput.Write($sql);$p.StandardInput.Close();$p.WaitForExit()
 $text=$output.GetAwaiter().GetResult();$null=$errors.GetAwaiter().GetResult()
 if($p.ExitCode){throw 'SQL step failed: preserve partial state; no automatic rollback/retry'}
 return $text.TrimEnd("`r","`n")
}
function Assert-Target {
 $listener=@(netstat -ano -p tcp|Select-String '^\s*TCP\s+127\.0\.0\.1:13306\s+\S+\s+LISTENING\s+\d+\s*$')
 if($listener.Count-ne 1){throw 'Isolated listener absent/ambiguous'}
 $serverPid=[int](($listener[0].Line.Trim()-split '\s+')[-1]);$process=Get-Process -Id $serverPid
 if($process.Path-ne 'C:\MAMP\bin\mysql\bin\mysqld.exe'){throw 'Wrong server process'}
 $identity=(Invoke-Client 'SELECT VERSION(),@@hostname,@@port,REPLACE(@@datadir,CHAR(92),''/''),DATABASE(),@@GLOBAL.sql_mode;')-split "`t"
 if($identity.Count-ne 6-or $identity[0]-ne '5.7.24'-or $identity[2]-ne '13306'-or
  $identity[3].TrimEnd('/')-ine "$repo/migrations/mt4/private/mt5a-mamp-13306/data".Replace('\','/')-or
  $identity[4]-cne 'electronic_register_test'){throw 'Wrong SQL identity'}
 return $identity[5]
}
function Validate-SqlFile([string]$file){
 $null=Assert-Target
 $result=Invoke-Client ([IO.File]::ReadAllText((Join-Path $PSScriptRoot $file)))
 $expected=switch($file){'000_preflight.sql'{7}'003_security_state_validate.sql'{6}'005_delete_contract_validate.sql'{2}default{throw 'Unknown validator'}}
 if(@($result-split "`n").Count-ne $expected){throw 'Incomplete validator output'}
 foreach($row in $result-split "`n"){
  $parts=$row.TrimEnd("`r")-split "`t"
  if($parts.Count-ne 2-or $parts[1]-ne '0'){throw "Validation failed: $file"}
 }
}
$globalBefore=Assert-Target
Validate-SqlFile '000_preflight.sql'
if($Mode-eq 'Apply'){
 if(@(Get-Process testhost -ErrorAction SilentlyContinue).Count){throw 'Quiesce writers before DDL'}
 if((Invoke-Client "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='institution_membership_security_state';")-ne '0'){throw 'EXPAND already present: never rerun or repair automatically'}
 if(!$BackupPath){throw 'New verified private backup path required'}
 $BackupPath=[IO.Path]::GetFullPath($BackupPath)
 $private=[IO.Path]::GetFullPath("$repo/migrations/mt4/private/mt5a-mamp-13306/")
 if(!$BackupPath.StartsWith($private,[StringComparison]::OrdinalIgnoreCase)-or $BackupPath.Contains('"')-or (Test-Path -LiteralPath $BackupPath)){throw 'Backup must be a new file under the existing ACL-protected private instance'}
 $null=Assert-Target
 $si=[Diagnostics.ProcessStartInfo]::new($Mysqldump);$si.UseShellExecute=$false;$si.CreateNoWindow=$true
 $si.RedirectStandardOutput=$true;$si.RedirectStandardError=$true
 $si.Arguments="--defaults-file=`"$ClientConfig`" --protocol=TCP --host=127.0.0.1 --port=13306 --single-transaction --skip-add-drop-table --column-statistics=0 --no-tablespaces --set-gtid-purged=OFF --routines --triggers --result-file=`"$BackupPath`" electronic_register_test"
 $si.EnvironmentVariables.Remove('MYSQL_PWD')
 $p=[Diagnostics.Process]::Start($si);$o=$p.StandardOutput.ReadToEndAsync();$e=$p.StandardError.ReadToEndAsync();$p.WaitForExit()
 $null=$o.GetAwaiter().GetResult();$null=$e.GetAwaiter().GetResult()
 if($p.ExitCode){throw 'Backup failed; no DDL executed'}
 $dump=[IO.File]::ReadAllText($BackupPath)
 $triggers=@([regex]::Matches($dump,'(?i)\bTRIGGER\s+`?([a-zA-Z0-9_]+)`?')|ForEach-Object {$_.Groups[1].Value}|Where-Object {$_-match '^mt[34]'}|Sort-Object -Unique).Count
 if(!$dump.Contains('Dump completed')-or [regex]::Matches($dump,'(?m)^CREATE TABLE `').Count-ne 32-or $triggers-ne 53){throw 'Backup integrity check failed'}
 Get-FileHash -LiteralPath $BackupPath
 foreach($file in @('001_security_state_expand.sql','002_security_state_backfill.sql')){
  if((Assert-Target)-cne $globalBefore){throw 'GLOBAL changed'}
  $null=Invoke-Client ([IO.File]::ReadAllText((Join-Path $PSScriptRoot $file)))
 }
 Validate-SqlFile '003_security_state_validate.sql'
 $null=Assert-Target
 $null=Invoke-Client ([IO.File]::ReadAllText((Join-Path $PSScriptRoot '004_membership_delete_error_contract.sql')))
}
Validate-SqlFile '003_security_state_validate.sql'
Validate-SqlFile '005_delete_contract_validate.sql'
if((Assert-Target)-cne $globalBefore){throw 'GLOBAL changed'}
Write-Output 'MT5B persistence validation passed. JWT/HTTP contract remains unchanged.'
