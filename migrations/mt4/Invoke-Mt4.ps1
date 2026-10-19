param([ValidateSet('expand','backfill','validate')][string]$Stage='validate')
$ErrorActionPreference='Stop'
$b=[System.Data.Common.DbConnectionStringBuilder]::new()
$b.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if ($b['Database'] -cne 'electronic_register_test' -or $b['Server'] -ne '127.0.0.1') { throw 'Explicit loopback electronic_register_test required.' }
$mysql='C:/xampp/mysql/bin/mysql.exe'
$dump='C:/xampp/mysql/bin/mysqldump.exe'
$arguments=@('--host=127.0.0.1',('--port='+$b['Port']),('--user='+$b['User']),'--database=electronic_register_test','--batch','--skip-column-names')
$previousPassword=$env:MYSQL_PWD
$env:MYSQL_PWD=$b['Password']
try {
 $db=& $mysql @arguments '--execute=SELECT DATABASE();'
 if ($LASTEXITCODE -ne 0 -or $db -cne 'electronic_register_test') { throw 'Database preflight failed.' }
 & $mysql @arguments '--execute=SELECT DATABASE(),VERSION(),@@SESSION.sql_mode,@@GLOBAL.sql_mode;'
 if ($LASTEXITCODE -ne 0) { throw 'Preflight failed.' }
 if ($Stage -eq 'expand') {
  $existing=& $mysql @arguments "--execute=SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('course_edition','course_edition_campus','student_enrollment');"
  $partial=& $mysql @arguments "--execute=SELECT (SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND index_name LIKE '%mt4%')+(SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='class' AND column_name='course_edition_id')+(SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_schema=DATABASE() AND trigger_name LIKE 'mt4_%');"
  $parents=& $mysql @arguments "--execute=SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND index_name IN ('uq_mt3a_student_owner','uq_mt3a_study_path_owner') AND non_unique=0 AND seq_in_index=1 AND column_name='institution_id';"
  if ($LASTEXITCODE -ne 0 -or $parents -ne '2') { throw 'MT3A parent keys missing or incompatible.' }
  if ($partial -ne '0' -or $existing -ne '0') { throw 'Existing or partial MT4 schema: inspect before retry; never reset.' }
  $private=Join-Path $PSScriptRoot 'private'
  New-Item -ItemType Directory -Force $private | Out-Null
  $backup=Join-Path $private ((Get-Date -Format 'yyyyMMdd-HHmmss-ffff')+'.sql')
  & $dump --host=127.0.0.1 ('--port='+$b['Port']) ('--user='+$b['User']) --single-transaction --routines --triggers --skip-add-drop-table ('--result-file='+$backup) electronic_register_test
  if ($LASTEXITCODE -ne 0) { throw 'Backup failed.' }
  $content=[IO.File]::ReadAllText($backup)
  if ($content.Length -lt 1000 -or -not $content.Contains('CREATE TABLE') -or -not $content.Contains('Dump completed')) { throw 'Incomplete backup.' }
  $tables=& $mysql @arguments "--execute=SELECT table_name FROM information_schema.tables WHERE table_schema=DATABASE() AND table_type='BASE TABLE';"
  if($LASTEXITCODE -ne 0){throw 'Backup inventory failed.'}
  foreach($table in $tables){if(-not $content.Contains(('CREATE TABLE `'+$table+'`'))){throw ('Backup misses table: '+$table)}}
  Get-FileHash -LiteralPath $backup -Algorithm SHA256 | Select-Object Hash
 }
 $file=switch($Stage){expand {'001_expand.sql'} backfill {'002_backfill.sql'} validate {'003_validate.sql'}}
 $path=(Join-Path $PSScriptRoot $file).Replace('\','/')
 $result=& $mysql @arguments "--execute=SET SESSION sql_mode=CONCAT_WS(',',@@SESSION.sql_mode,'STRICT_TRANS_TABLES'); SOURCE $path;"
 $commandExit=$LASTEXITCODE
 $result | Write-Output
 if($Stage -eq 'validate'){
  if(@($result).Count -ne 7){throw 'Incomplete MT4 validation result.'}
  foreach($line in $result){$parts=$line -split "`t";if($parts.Count -ne 2 -or $parts[1] -ne '0'){throw ('MT4 integrity violation: '+$line)}}
 }
 if ($commandExit -ne 0) { throw 'Migration stopped. DDL may be partial; inspect. No automatic rollback or destructive recovery.' }
} finally { $env:MYSQL_PWD=$previousPassword }
