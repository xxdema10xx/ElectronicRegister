$ErrorActionPreference='Stop'
$b=[System.Data.Common.DbConnectionStringBuilder]::new()
$b.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if ($b.get_Item('Database') -cne 'electronic_register_test' -or $b.get_Item('Server') -ne '127.0.0.1') { throw 'Only explicit local test database.' }
$mysql='C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe'
$mysqlArgs=@('--host=127.0.0.1',('--port='+$b.get_Item('Port')),('--user='+$b.get_Item('User')),'--database=electronic_register_test','--batch','--skip-column-names','--raw')
$env:MYSQL_PWD=$b.get_Item('Password')
try {
 function Query([string]$sql) {
  $value=& $mysql @mysqlArgs ('--execute='+$sql)
  if ($LASTEXITCODE -ne 0) { throw 'Metadata query failed.' }
  return $value
 }
 if ((Query 'SELECT DATABASE()') -cne 'electronic_register_test') { throw 'Wrong selected database.' }
 $state=Query "SELECT JSON_OBJECT('database',DATABASE(),'version',VERSION(),'sqlMode',@@sql_mode,'institutions',(SELECT COUNT(*) FROM institution),'nonHoldingInstitutions',(SELECT COUNT(*) FROM institution WHERE id<>'4d543341-0000-4000-8000-000000000001'),'holdingStatus',(SELECT status FROM institution WHERE id='4d543341-0000-4000-8000-000000000001'),'memberships',(SELECT COUNT(*) FROM institution_membership),'studentProfileLinks',(SELECT COUNT(*) FROM institution_student_profile),'teacherProfileLinks',(SELECT COUNT(*) FROM institution_teacher_profile),'auditWithoutOwner',(SELECT COUNT(*) FROM audit_event WHERE entity_type<>'platform' AND institution_id IS NULL))" | ConvertFrom-Json
 $owners=[ordered]@{}
 foreach ($table in @('biennium','study_area','study_path','subject','teacher','student','biennium_study_area','biennium_study_path','class','class_subject','exam','exam_student','grade','exam_submission','question','question_option','submission_answer','submission_answer_option','exam_violation')) {
  $data=Query ("SELECT JSON_OBJECT('count',COUNT(*),'unowned',COUNT(CASE WHEN institution_id IS NULL OR institution_id='00000000-0000-0000-0000-000000000000' THEN 1 END),'outsideHolding',COUNT(CASE WHEN institution_id<>'4d543341-0000-4000-8000-000000000001' THEN 1 END)) FROM ``"+$table+'`') | ConvertFrom-Json
  if ($data.unowned -ne 0 -or $data.outsideHolding -ne 0) { throw ('Unexpected owner state '+$table) }
  $owners[$table]=$data
 }
 if ($state.nonHoldingInstitutions -ne 0 -or $state.holdingStatus -ne 'provisioning' -or $state.memberships -ne 0 -or $state.studentProfileLinks -ne 0 -or $state.teacherProfileLinks -ne 0 -or $state.auditWithoutOwner -ne 0) { throw 'Unexpected remaining tenant fixture or unowned audit.' }
 @{DatabaseState=$state;SchoolTables=$owners;NoOperationalSecondTenant=$true} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mt3a-evidence/final-state.json')
 Write-Output 'Only explicit provisioning holding remains; zero memberships/profile links, no unowned school rows or school audit.'
} finally { Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue }
