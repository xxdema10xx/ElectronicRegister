param([string]$OutputName = 'server-before.txt')
$ErrorActionPreference = 'Stop'
$parts = @{}
foreach ($part in $env:TEST_MYSQL_CONNECTION.Split(';')) {
    if ($part -match '^\s*([^=]+)=(.*)$') { $parts[$matches[1].Trim()] = $matches[2].Trim() }
}
if ($parts['Database'] -ne 'electronic_register_test' -or $parts['Server'] -notin @('127.0.0.1','localhost','::1')) {
    throw 'Only explicit loopback electronic_register_test is permitted.'
}
$testPort = if ($parts['Port']) { [int]$parts['Port'] } else { 3306 }
$testUser = $parts['User Id']; if (!$testUser) { $testUser = $parts['Uid'] }; if (!$testUser) { $testUser = $parts['User'] }
$testPassword = $parts['Password']; if (!$testPassword) { $testPassword = $parts['Pwd'] }
$previousPassword = $env:MYSQL_PWD
$sql = @'
SELECT VERSION() AS version,@@version_comment AS version_comment,@@hostname AS hostname,@@port AS port,@@datadir AS datadir,DATABASE() AS selected_database,@@global.sql_mode AS global_sql_mode,@@session.sql_mode AS session_sql_mode;
SELECT table_name,engine FROM information_schema.tables WHERE table_schema=DATABASE() ORDER BY table_name;
SELECT table_name,column_name,column_type,is_nullable,column_default,extra FROM information_schema.columns WHERE table_schema=DATABASE() ORDER BY table_name,ordinal_position;
SELECT table_name,index_name,non_unique,seq_in_index,column_name FROM information_schema.statistics WHERE table_schema=DATABASE() ORDER BY table_name,index_name,seq_in_index;
SELECT table_name,constraint_name,column_name,referenced_table_name,referenced_column_name,ordinal_position FROM information_schema.key_column_usage WHERE constraint_schema=DATABASE() ORDER BY table_name,constraint_name,ordinal_position;
SELECT table_name,constraint_name,referenced_table_name,update_rule,delete_rule FROM information_schema.referential_constraints WHERE constraint_schema=DATABASE() ORDER BY table_name,constraint_name;
SELECT trigger_name,event_object_table,action_timing,event_manipulation,action_statement FROM information_schema.triggers WHERE trigger_schema=DATABASE() ORDER BY trigger_name;
SELECT code,status FROM institution ORDER BY code;
SELECT (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()) AS tables_count,(SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_schema=DATABASE()) AS trigger_count,(SELECT COUNT(*) FROM user) AS user_count,(SELECT COUNT(*) FROM institution_membership) AS membership_count,(SELECT COUNT(*) FROM institution_membership_role) AS role_count;
SELECT table_name,column_name,column_type FROM information_schema.columns WHERE table_schema=DATABASE() AND ((table_name='student_enrollment' AND column_name='revision') OR (table_name='class' AND column_name='course_edition_id'));
SHOW CREATE TABLE institution_membership;
SHOW CREATE TABLE institution_membership_role;
SHOW CREATE TABLE class;
'@
try {
    $env:MYSQL_PWD = $testPassword
    & C:/MAMP/bin/mysql/bin/mysql.exe --protocol=TCP --host=$($parts['Server']) --port=$testPort --user=$testUser --database=electronic_register_test --batch --execute=$sql |
        Set-Content -LiteralPath (Join-Path $PSScriptRoot $OutputName)
    if ($LASTEXITCODE -ne 0) { throw 'Read-only SQL inspection failed.' }
} finally {
    if ($null -eq $previousPassword) { Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue }
    else { $env:MYSQL_PWD = $previousPassword }
}
