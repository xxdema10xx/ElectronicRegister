param(
 [Parameter(Mandatory=$true)][ValidateSet('expand','enforce','audit-platform','prune-indexes','prune-fk-indexes')][string]$Stage,
 [Parameter(Mandatory=$true)][Guid]$LegacyInstitutionId
)
$ErrorActionPreference='Stop'
$b=[System.Data.Common.DbConnectionStringBuilder]::new()
$b.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if ($b.get_Item('Database') -cne 'electronic_register_test' -or $b.get_Item('Server') -ne '127.0.0.1' -or $LegacyInstitutionId -eq [Guid]::Empty) { throw 'Explicit local test connection/legacy manifest required.' }
$mysql='C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe'
$dump='C:/Program Files/MySQL/MySQL Server 8.0/bin/mysqldump.exe'
$private=Join-Path $PSScriptRoot 'mt3a-evidence/private'
New-Item -ItemType Directory -Force $private | Out-Null
$backup=Join-Path $private ((Get-Date -Format 'yyyyMMdd-HHmmss-ffff')+'-'+$Stage+'.sql')
$env:MYSQL_PWD=$b.get_Item('Password')
try {
 $mysqlArgs=@('--host=127.0.0.1',('--port='+$b.get_Item('Port')),('--user='+$b.get_Item('User')),'--database=electronic_register_test','--batch','--skip-column-names')
 $selected=& $mysql @mysqlArgs '--execute=SELECT DATABASE();'
 if ($LASTEXITCODE -ne 0 -or $selected -cne 'electronic_register_test') { throw 'Wrong selected database.' }
 & $mysql @mysqlArgs '--execute=SELECT DATABASE(),VERSION(),@@SESSION.sql_mode;' | Tee-Object -FilePath (Join-Path $PSScriptRoot ('mt3a-evidence/preflight-'+$Stage+'.txt'))
 if ($LASTEXITCODE -ne 0) { throw 'Preflight failed.' }
 & $dump --host=127.0.0.1 ('--port='+$b.get_Item('Port')) ('--user='+$b.get_Item('User')) --single-transaction --column-statistics=0 --no-tablespaces --set-gtid-purged=OFF --skip-add-drop-table ('--result-file='+$backup) electronic_register_test
 if ($LASTEXITCODE -ne 0) { throw 'Backup export failed.' }
 $content=[IO.File]::ReadAllText($backup)
 if ((Get-Item -LiteralPath $backup).Length -lt 1000 -or -not $content.Contains('CREATE TABLE') -or -not $content.Contains('Dump completed')) { throw 'Backup unreadable/incomplete.' }
 (Get-FileHash -LiteralPath $backup).Hash | Set-Content (Join-Path $PSScriptRoot ('mt3a-evidence/backup-'+$Stage+'-sha256.txt'))
 & dotnet run --no-build --project tools/Mt3aMigration -- $Stage $LegacyInstitutionId $backup
 if ($LASTEXITCODE -ne 0) { throw 'Migration stopped; inspect partial DDL. No automatic DROP or full rollback.' }
} finally { Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue }
