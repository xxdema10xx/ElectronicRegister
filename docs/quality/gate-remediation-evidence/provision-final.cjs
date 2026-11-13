// Apply-once verification bootstrap for the explicitly NEW instance in instance.json.
const fs=require('fs'),cp=require('child_process'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'../../..'),i=JSON.parse(fs.readFileSync(__dirname+'/instance.json','utf8').replace(/^\uFEFF/,'')),run=path.resolve(i.Run);
if(!i.New||!run.startsWith(path.join(root,'.staff-private','instance-'))||[3306,13306].includes(i.Port))throw Error('New disposable target required');
let password=process.argv.includes('--finish')?fs.readFileSync(path.join(run,'password.txt'),'utf8'):undefined;
const client='C:/MAMP/bin/mysql/bin/mysql.exe',args=['--no-defaults','--protocol=TCP','--host=127.0.0.1','--port='+i.Port,'--user=root','--batch','--raw','--skip-column-names','--default-character-set=utf8mb4'];
function query(sql,database=true){const p=cp.spawnSync(client,[...args,...database?['--database=electronic_register_test']:[]],{env:{...process.env,MYSQL_PWD:password||''},input:sql,encoding:'utf8'});if(p.status!==0){fs.appendFileSync(path.join(run,'provision-error.log'),p.stderr);throw Error('SQL failed; preserve state, never replay');}return p.stdout.trim();}
const [version,port,data]=query("SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/');",false).split('\t');
if(version!=='5.7.24'||+port!==i.Port||data.replace(/\/$/,'').toLowerCase()!==path.join(run,'data').replaceAll('\\','/').toLowerCase())throw Error('SQL identity mismatch');
if(!process.argv.includes('--finish')){
 if(fs.existsSync(path.join(run,'password.txt')))throw Error('Already provisioned; no replay');
 const fresh=crypto.randomBytes(32).toString('hex');
 query(`ALTER USER 'root'@'localhost' IDENTIFIED BY '${fresh}'; CREATE DATABASE electronic_register_test CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE USER 'er_test'@'localhost'; GRANT ALL PRIVILEGES ON electronic_register_test.* TO 'er_test'@'localhost';`,false);
 password=fresh;fs.writeFileSync(path.join(run,'password.txt'),password,{flag:'wx'});
 query(fs.readFileSync(path.join(root,'migrations/bootstrap/001_schema.sql'),'utf8'));
 for(const f of ['001_identity_account.sql','002_profile_acquisition.sql'])query(fs.readFileSync(path.join(root,'migrations/identity-account',f),'utf8'));
 fs.writeFileSync(path.join(run,'bootstrap-phase1.done'),'complete',{flag:'wx'});
 console.log('Fresh bootstrap + identity 001/002 complete; schema deliberately incomplete for readiness verification.');
}else{
 if(!fs.existsSync(path.join(run,'bootstrap-phase1.done'))||fs.existsSync(path.join(run,'bootstrap-phase2.started')))throw Error('Invalid stage or attempted replay');
 fs.writeFileSync(path.join(run,'bootstrap-phase2.started'),'started',{flag:'wx'});
 for(const f of ['003_password_attempt.sql','004_release_schema.sql','005_release_gate.sql']){
  if(f==='005_release_gate.sql'){
   if(query('CALL er_schema_ready();')!=='1')throw Error('Prior gate setup invalid');
   const probe=cp.spawnSync('node',[__dirname+'/readiness-process.cjs','503','prior-gate-revision'],{cwd:root,encoding:'utf8'});
   if(probe.status)throw Error('API accepted prior gate revision');
   fs.writeFileSync(__dirname+'/prior-gate-negative.json',JSON.stringify({Prior004RoutineReturns:1,NewApiReadiness:503,NewApiLiveness:200,Missing005MarkerRejected:true,Port:i.Port},null,2));
  }
  query(fs.readFileSync(path.join(root,'migrations/identity-account',f),'utf8'));
 }
 if(query('CALL er_schema_ready();')!=='1')throw Error('Incomplete schema');
 query("INSERT INTO institution(id,name,code,status) VALUES ('4d543341-0000-4000-8000-000000000001','Technical legacy test holding','MT3A_LEGACY_TEST','provisioning'); INSERT INTO mt3a_legacy_scope(scope,institution_id) VALUES ('legacy','4d543341-0000-4000-8000-000000000001');");
 if(query('SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();')!=='0')throw Error('Active test writers; stop before binding guards');
 const contract=JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json'),'utf8'));
 for(const [name,t]of Object.entries(contract.triggers)){
  if(!t.statement.includes('@@port=13306'))continue;
  const old=contract.trigger_metadata[name].ACTION_STATEMENT.replaceAll("'","''");
  if(query(`SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME='${name}' AND BINARY ACTION_STATEMENT=BINARY '${old}';`)!=='1')throw Error('Unexpected retention guard');
  const replacement=t.statement.replaceAll('@@port=13306','@@port='+i.Port).replaceAll("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'",`LOWER(REPLACE(@@datadir,CHAR(92),'/'))='${data.toLowerCase().replaceAll("'","''")}'`);
  query(`SET SESSION sql_mode='${t.mode}';\nSET NAMES ${t.charset} COLLATE ${t.collation};\nDROP TRIGGER \`${name}\`;\nDELIMITER $$\n${replacement}$$\nDELIMITER ;`);
 }
 fs.writeFileSync(path.join(run,'identity.tsv'),query('SELECT VERSION(),@@port,@@datadir;')+'\n',{flag:'wx'});
 console.log('Final additive closure and exact disposable retention guards verified.');
}
