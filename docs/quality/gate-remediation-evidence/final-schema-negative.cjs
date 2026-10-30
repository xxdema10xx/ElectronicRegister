// Re-run the original counterexample against the final API binary after full regression.
const fs=require('fs'),cp=require('child_process'),{execute,sql,root,run,i}=require('./sql.cjs');
const config=run+'/final-schema-preflight.json';fs.writeFileSync(config,JSON.stringify({host:'127.0.0.1',port:i.Port,database:'electronic_register_test',user:'root',passwordFile:run+'/password.txt',expectedVersion:'5.7.24',expectedDataDir:run+'/data',mysqlClient:'C:/MAMP/bin/mysql/bin/mysql.exe'}),{flag:'wx'});
function preflight(){const p=cp.spawnSync('node',['tools/release-preflight.cjs',config,'--isolated-test'],{cwd:root,encoding:'utf8'});return {exit:p.status,stdout:p.stdout.trim(),error:p.stderr.trim().split('\n').find(s=>s.startsWith('Error:'))||''};}
const result={before:preflight(),port:i.Port};if(result.before.exit)throw Error('Final complete schema preflight failed: '+result.before.error);
execute("CREATE USER 'er_final_api'@'localhost'; GRANT SELECT,INSERT,UPDATE,DELETE ON electronic_register_test.* TO 'er_final_api'@'localhost'; GRANT EXECUTE ON PROCEDURE electronic_register_test.er_schema_ready TO 'er_final_api'@'localhost'");
// Retain even synthetic quota timestamps instead of losing them during DROP/ADD.
const backup=JSON.parse(execute("SELECT JSON_ARRAYAGG(JSON_OBJECT('id',user_id,'lease',DATE_FORMAT(lease_until,'%Y-%m-%d %H:%i:%s.%f'))) FROM identity_password_attempt"))||[];
execute('ALTER TABLE identity_password_attempt DROP COLUMN lease_until');
try{
 result.missingColumn=preflight();result.routine=sql('CALL er_schema_ready();').stdout;
 const probe=cp.spawnSync('node',[__dirname+'/readiness-process.cjs','503','lease-until-final','er_final_api'],{cwd:root,encoding:'utf8'});result.httpExit=probe.status;
 if(result.missingColumn.exit===0||result.routine!=='0'||probe.status!==0)throw Error('Final original negative control accepted');
}finally{
 execute('ALTER TABLE identity_password_attempt ADD COLUMN lease_until datetime(6) DEFAULT NULL AFTER lease_id');
 for(const row of backup.filter(r=>r.lease!==null)){if(!/^[0-9a-f-]{36}$/i.test(row.id)||!/^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{6}$/.test(row.lease))throw Error('Unexpected backup format');execute(`UPDATE identity_password_attempt SET lease_until='${row.lease}' WHERE user_id='${row.id}'`);}
}
result.restored=preflight();if(result.restored.exit)throw Error('Final schema restoration failed');
const positive=cp.spawnSync('node',[__dirname+'/readiness-process.cjs','200','schema-final-restored','er_final_api'],{cwd:root,encoding:'utf8'});if(positive.status)throw Error('Final API schema positive failed');
fs.writeFileSync(__dirname+'/final-schema-negative.json',JSON.stringify(result,null,2));console.log('Final binary: missing lease_until -> 503/preflight rejection; complete schema -> 200/preflight pass; non-root API, no DDL grants.');
