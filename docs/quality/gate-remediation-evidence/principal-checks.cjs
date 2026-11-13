const fs=require('fs'),cp=require('child_process'),{execute,sql,root,run,i}=require('./sql.cjs');
if(i.Port!==13451)throw Error('Principal verification instance required');
// Bind only historical fixture retention guards, preserving the explicit owner.
// These scoped test-only deltas are never emitted by the release bundler.
const contract=JSON.parse(fs.readFileSync(root+'/migrations/bootstrap/schema-contract.json','utf8'));
const data=(run+'/data/').replaceAll('\\','/').toLowerCase();
for(const [name,t]of Object.entries(contract.triggers))if(t.statement.includes('@@port=13306')){
 const statement=t.statement.replace(/^CREATE DEFINER=`[^`]+`@`[^`]+` TRIGGER /,"CREATE DEFINER='er_gate_definer'@'localhost' TRIGGER ").replaceAll('@@port=13306','@@port='+i.Port).replaceAll("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'",`LOWER(REPLACE(@@datadir,CHAR(92),'/'))='${data}'`);
 execute(`DROP TRIGGER \`${name}\`; SET SESSION sql_mode='${t.mode}';\nDELIMITER $$\n${statement}$$\nDELIMITER ;`);
}
const pwd=run+'/review-password.txt';fs.writeFileSync(pwd,'',{flag:'wx'});
const config=run+'/principal-preflight.json';fs.writeFileSync(config,JSON.stringify({host:'127.0.0.1',port:i.Port,database:'electronic_register_test',user:'er_gate_review',passwordFile:pwd,expectedVersion:'5.7.24',expectedDataDir:run+'/data',schemaDefiner:'er_gate_definer@localhost',mysqlClient:'C:/MAMP/bin/mysql/bin/mysql.exe'}),{flag:'wx'});
function preflight(){const p=cp.spawnSync('node',['tools/release-preflight.cjs',config,'--isolated-test'],{cwd:root,encoding:'utf8'});return {exit:p.status,stdout:p.stdout.trim(),error:p.stderr.trim().split('\n').find(s=>s.startsWith('Error:'))||''};}
function http(expected,name){const p=cp.spawnSync('node',[__dirname+'/readiness-process.cjs',String(expected),name,'er_gate_api'],{cwd:root,encoding:'utf8'});if(p.status)throw Error('Unexpected HTTP readiness: '+name);}
const result={baseline:preflight(),apiDdlDenied:[]};if(result.baseline.exit)throw Error('Baseline preflight failed: '+result.baseline.error);
http(200,'explicit-definer-complete');
for(const query of ['CREATE TABLE er_forbidden(id int)',"ALTER TABLE er_schema_release COMMENT='forbidden'",'DROP TABLE er_schema_release']){
 const p=sql(query,{user:'er_gate_api',password:''});if(!p.exit)throw Error('API DDL granted');result.apiDdlDenied.push(true);
}
execute("REVOKE EXECUTE ON PROCEDURE electronic_register_test.er_schema_ready FROM 'er_gate_api'@'localhost'");
try{http(503,'explicit-api-no-execute');result.noExecuteRejected=true;}finally{execute("GRANT EXECUTE ON PROCEDURE electronic_register_test.er_schema_ready TO 'er_gate_api'@'localhost'");}
execute("REVOKE TRIGGER ON electronic_register_test.* FROM 'er_gate_definer'@'localhost'");
try{result.noMetadata=preflight();if(!result.noMetadata.exit)throw Error('Definer without metadata passed');http(503,'explicit-definer-no-trigger');}finally{execute("GRANT TRIGGER ON electronic_register_test.* TO 'er_gate_definer'@'localhost'");}
execute("DROP USER 'er_gate_definer'@'localhost'");
try{result.missingDefiner=preflight();if(!result.missingDefiner.exit)throw Error('Missing definer passed');http(503,'explicit-definer-missing');}
finally{execute("CREATE USER 'er_gate_definer'@'localhost' ACCOUNT LOCK; GRANT SELECT,INSERT,UPDATE,DELETE,TRIGGER ON electronic_register_test.* TO 'er_gate_definer'@'localhost'; GRANT EXECUTE ON PROCEDURE electronic_register_test.er_schema_ready TO 'er_gate_definer'@'localhost'");}
result.restored=preflight();if(result.restored.exit)throw Error('Principal restoration failed');http(200,'explicit-definer-restored');
fs.writeFileSync(__dirname+'/principal-checks.json',JSON.stringify(result,null,2));console.log('Explicit locked definer + non-root API/reviewer verified; missing principal/metadata/EXECUTE rejected; API DDL denied.');
