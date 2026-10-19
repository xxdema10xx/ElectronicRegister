$ErrorActionPreference='Stop'
$connection=[System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if ($connection.get_Item('Database') -cne 'electronic_register_test' -or $connection.get_Item('Server') -ne '127.0.0.1') { throw 'Only authorized local test database.' }
$mysql='C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe'
$dump='C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe'
$env:MYSQL_PWD=$connection.get_Item('Password')
try {
 $selected=& $mysql --host=127.0.0.1 --port=3306 --user=er_test --database=electronic_register_test --batch --skip-column-names --execute='SELECT DATABASE();'
 if ($LASTEXITCODE -ne 0 -or $selected -cne 'electronic_register_test') { throw 'Wrong database.' }
 $backup=Join-Path $PSScriptRoot 'mt3-evidence/private/before-profile-expand.sql'
 if (Test-Path -LiteralPath $backup) { throw 'Backup already exists; never overwrite.' }
 & $dump --host=127.0.0.1 --port=3306 --user=er_test --single-transaction --column-statistics=0 --no-tablespaces --set-gtid-purged=OFF --skip-add-drop-table "--result-file=$backup" electronic_register_test
 if ($LASTEXITCODE -ne 0 -or (Get-Item -LiteralPath $backup).Length -lt 1000) { throw 'Backup failed.' }
 $backupContent=[IO.File]::ReadAllText($backup)
 if (-not $backupContent.Contains('CREATE TABLE') -or -not $backupContent.Contains('Dump completed')) { throw 'Unreadable/incomplete export.' }
 (Get-FileHash -LiteralPath $backup).Hash | Set-Content (Join-Path $PSScriptRoot 'mt3-evidence/backup-sha256.txt')
 $sql=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'mt3-staging/sql/001_profile_expand.sql'))
 $sql | & $mysql --host=127.0.0.1 --port=3306 --user=er_test --database=electronic_register_test
 if ($LASTEXITCODE -ne 0) { throw 'DDL failed; inspect actual schema and backup. No atomic rollback promised.' }
} finally { Remove-Item Env:MYSQL_PWD }
