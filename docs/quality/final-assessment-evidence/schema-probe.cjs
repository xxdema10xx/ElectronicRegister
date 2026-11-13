// Destructive negative control ONLY on the new, identity-attested disposable instance.
const fs=require('fs'),cp=require('child_process'),path=require('path');
const root=path.resolve(__dirname,'../../..'),i=JSON.parse(fs.readFileSync(__dirname+'/instance.json','utf8').replace(/^\uFEFF/,''));
const run=path.resolve(i.Run);if(!i.New||!run.startsWith(path.join(root,'.staff-private','instance-'))||i.Port!==13449)throw Error('Wrong target');
const password=fs.readFileSync(run+'/password.txt','utf8'),client='C:/MAMP/bin/mysql/bin/mysql.exe';
function sql(s){const p=cp.spawnSync(client,['--no-defaults','--protocol=TCP','--host=127.0.0.1','--port='+i.Port,'--user=root','--database=electronic_register_test','--batch','--raw','--skip-column-names'],{input:s,encoding:'utf8',env:{...process.env,MYSQL_PWD:password}});return {exit:p.status,stdout:p.stdout.trim(),stderr:p.stderr.trim()};}
const id=sql("SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/');");const [version,port,data]=id.stdout.split('\t');
if(id.exit||version!=='5.7.24'||+port!==i.Port||data.replace(/\/$/,'').toLowerCase()!==path.join(run,'data').replaceAll('\\','/').toLowerCase())throw Error('Identity mismatch');
const config=run+'/assessment-preflight.json';fs.writeFileSync(config,JSON.stringify({host:'127.0.0.1',port:i.Port,database:'electronic_register_test',user:'root',passwordFile:run+'/password.txt',expectedVersion:version,expectedDataDir:data,mysqlClient:client}),{flag:'wx'});
function preflight(){const p=cp.spawnSync('node',['tools/release-preflight.cjs',config,'--isolated-test'],{cwd:root,encoding:'utf8'});return {exit:p.status,stdout:p.stdout.trim(),stderr:p.stderr.trim()};}
const result={identity:{version,port:+port},complete:sql('CALL er_schema_ready();'),completePreflight:preflight()};
if(result.complete.stdout!=='1'||result.completePreflight.exit)throw Error('Baseline not complete');
if(sql('ALTER TABLE identity_password_attempt DROP COLUMN lease_until;').exit)throw Error('Negative control failed');
try{result.missingRequiredColumn={probe:sql('CALL er_schema_ready();'),preflight:preflight(),passwordQuery:sql('SELECT lease_until FROM identity_password_attempt LIMIT 0;')};
 const p=cp.spawnSync('node',[__dirname+'/readiness-process.cjs','200','missing-quota-column'],{cwd:root,encoding:'utf8'});result.realProgram={exit:p.status,output:p.stdout.trim(),error:p.stderr.trim()};
}finally{result.restored=sql('ALTER TABLE identity_password_attempt ADD COLUMN lease_until datetime(6) DEFAULT NULL;');}
fs.writeFileSync(__dirname+'/schema-negative-control.json',JSON.stringify(result,null,2));
if(result.restored.exit)throw Error('Restoration failed');console.log('Required-column negative control recorded; original additive schema restored.');
