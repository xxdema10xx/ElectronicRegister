{
// Execute the production preflight against deterministic MySQL responses.
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert');
const root=path.resolve(__dirname,'..'),file=path.join(__dirname,'release-preflight.cjs');
const base=require('../migrations/bootstrap/schema-contract.json'),add=require('./release-additive-contract.cjs').forMySql84(),adapter=require('./mysql84-contract.cjs');
const config={caFile:'ca',passwordFile:'password',user:'review',schemaDefiner:'owner@%',host:'host',port:3306,database:'db',expectedVersion:'8.4.13',expectedDataDir:'/data/'};
function simulate(mutant){
 const output=[];
 function read(sql){
  if(sql.startsWith('SELECT VERSION'))return '8.4.13\t3306\t/data/\tdb';
  if(sql.startsWith('SHOW CREATE TABLE')){const name=/`([^`]+)`/.exec(sql)[1];return name+'\t'+((base.tables[name]||add.contract.tables[name]).replace(/ COLLATE utf8mb4_unicode_ci/g,' CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci')+(mutant==='table'?' SQL secret-password':''));}
  if(sql.startsWith('SHOW CREATE TRIGGER')){const name=/`([^`]+)`/.exec(sql)[1],t=base.triggers[name];return [name,adapter.mode(t.mode),t.statement+(mutant==='trigger'?' SQL secret-password':''),t.charset,t.collation,t.database_collation,'timestamp'].join('\t');}
  if(sql.includes('information_schema.TRIGGERS'))return JSON.stringify([
   ...Object.keys(base.triggers).map(name=>({name,definer:mutant==='trigger-definer'?'secret-password':config.schemaDefiner})),
   ...add.contract.triggers.map(t=>({name:t.TRIGGER_NAME,table:t.EVENT_OBJECT_TABLE,event:t.EVENT_MANIPULATION,timing:t.ACTION_TIMING,body:t.ACTION_STATEMENT,mode:t.SQL_MODE,definer:config.schemaDefiner}))]);
  if(sql.includes('information_schema.ROUTINES'))return JSON.stringify({body:add.body+(mutant==='routine'?' SQL secret-password':''),security:'DEFINER',access:'READS SQL DATA',definer:mutant==='definer'?'secret-password':config.schemaDefiner});
  return '1';
 }
 const context={__dirname,require:id=>id==='fs'?{readFileSync:(f,...args)=>f==='config'?JSON.stringify(config):f==='password'?'secret-password':fs.readFileSync(f,...args)}:id==='child_process'?{spawnSync:(_cmd,_args,options)=>({status:0,stdout:read(options.input)})}:require(id),console:{error:s=>output.push(s),log:()=>{}},process:{argv:['node',file,'config'],env:{}}};
 vm.runInNewContext(fs.readFileSync(file,'utf8'),context);
 if(!mutant){assert.equal(context.process.exitCode,undefined);assert.equal(output.length,0);return;}
 assert.equal(context.process.exitCode,1);assert.equal(output.length,1);assert(!output[0].includes('secret-password'));assert(!output[0].includes('CREATE '));
 return JSON.parse(output[0].replace('ER_PREFLIGHT_FAILURE ',''));
}
simulate();
const failures=['table','trigger','routine','definer','trigger-definer'].map(simulate);
assert.deepEqual(failures.map(f=>f.id),['base-table-ddl','base-trigger-ddl','routine-body','routine-definer','trigger-definer']);
assert.equal(failures[0].object,Object.keys(base.tables)[0]);assert.equal(failures[1].object,Object.keys(base.triggers)[0]);
for(const f of failures.slice(0,3)){assert.match(f.actualHash,/^[A-F0-9]{64}$/);assert.notEqual(f.actualHash,f.expectedHash);}
if(process.argv.includes('--diagnostics-json')){console.log(JSON.stringify(failures));process.exit(0);}

}
// Offline compatibility checks, including negative controls. No server contacted.
const fs=require('fs'),os=require('os'),path=require('path'),assert=require('assert'),cp=require('child_process'),crypto=require('crypto');
const adapter=require('./mysql84-contract.cjs'),root=path.resolve(__dirname,'..');
const old=require('./release-additive-contract.cjs'),modern=old.forMySql84();
assert(old.contract.triggers.every(t=>t.SQL_MODE.includes('NO_AUTO_CREATE_USER')));
assert(modern.contract.triggers.every(t=>!t.SQL_MODE.includes('NO_AUTO_CREATE_USER')));
assert.deepStrictEqual(modern.contract.triggers.map(t=>t.ACTION_STATEMENT),old.contract.triggers.map(t=>t.ACTION_STATEMENT));
for(const key of ['indexes','foreignKeys','keyColumns','tables'])assert.deepStrictEqual(modern.contract[key],old.contract[key]);
assert.equal(adapter.column('int(11) unsigned'), 'int unsigned');
assert.equal(adapter.column('tinyint(1)'), 'tinyint(1)');
assert.equal(adapter.sql('row_number int, PRIMARY KEY(row_number); IF NEW.row_number<1'), '`row_number` int, PRIMARY KEY(`row_number`); IF NEW.row_number<1');
for(const column of modern.contract.columns.filter(c=>c.COLUMN_DEFAULT==='CURRENT_TIMESTAMP(6)'))assert.equal(column.EXTRA,'DEFAULT_GENERATED');
const expected='`owner` char(36) CHARACTER SET ascii NOT NULL, UNIQUE KEY `tenant` (`institution_id`,`id`)';
const explicit='  `action` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL';
assert.equal(adapter.ddl(explicit.replace(' COLLATE',' CHARACTER SET utf8mb4 COLLATE')),explicit);
for(const changed of [explicit.replace('unicode_ci','0900_ai_ci'),explicit.replace('varchar(100)','varchar(101)'),explicit.replace('NOT NULL','DEFAULT NULL')])assert.notEqual(adapter.ddl(changed),adapter.ddl(explicit));
const literal="  `action` varchar(100) DEFAULT ' CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci'";
assert.equal(adapter.ddl(literal),literal);
assert.equal(adapter.ddl(expected.replace('CHARACTER SET ascii','CHARACTER SET ascii COLLATE ascii_general_ci')),expected);
for(const changed of [expected.replace('ascii','utf8mb4'),expected.replace('NOT NULL','NULL'),expected.replace('UNIQUE KEY','KEY'),expected.replace('`institution_id`,`id`','`id`,`institution_id`')])assert.notEqual(adapter.ddl(changed),adapter.ddl(expected));
assert.notEqual(adapter.ddl('COLLATE=utf8mb4_unicode_ci'),adapter.ddl('COLLATE=utf8mb4_0900_ai_ci'));
const base=JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json')));
const tables={...base.tables,...old.contract.tables};let fks=0;
const key=s=>s.replaceAll(' ','');
for(const [name,ddl]of Object.entries(tables))for(const fk of ddl.matchAll(/FOREIGN KEY \(([^)]+)\) REFERENCES `([^`]+)` \(([^)]+)\)/g)){
 const unique=[...tables[fk[2]].matchAll(/(?:PRIMARY KEY|UNIQUE KEY `[^`]+`) \(([^)]+)\)/g)].map(m=>key(m[1]));
 assert(unique.includes(key(fk[3])),`${name}: nonstandard referenced key`);fks++;
}
const temp=fs.mkdtempSync(path.join(os.tmpdir(),'er-mysql84-'));
// Exercise the actual preflight child failure path without a database or shell.
{
 const vm=require('vm'),file=path.join(root,'tools/release-preflight.cjs');
 for(const stderr of ['ERROR 1142 (42000) at line 7: fake-password CREATE USER secret;', 'unstructured fake-password']){
  const output=[],stop=new Error('test exit');
  const config={caFile:'fake-ca',passwordFile:'fake-password-file',user:'review',schemaDefiner:'owner@%',host:'host',port:3306,database:'db'};
  const context={require:id=>id==='fs'?{readFileSync:f=>f==='config'?JSON.stringify(config):'fake-password'}:id==='child_process'?{spawnSync:()=>({status:1,stderr})}:require(id),__dirname:path.dirname(file),console:{error:s=>output.push(s)},process:{argv:['node',file,'config'],env:{},exit:()=>{throw stop;}}};
  vm.runInNewContext(fs.readFileSync(file,'utf8'),context);assert.equal(context.process.exitCode,1);
  assert.equal(output.length,1);assert(!output[0].includes('fake-password'));assert(!output[0].includes('CREATE USER'));
  const data=JSON.parse(output[0].replace('ER_SQL_FAILURE ',''));assert.equal(data.step,'preflight-query-1');assert.equal(data.exitCode,1);
  assert.equal(data.mysqlErrno,stderr.startsWith('ERROR')?1142:null);assert.equal(data.sqlstate,stderr.startsWith('ERROR')?'42000':null);assert.equal(data.line,stderr.startsWith('ERROR')?7:null);
 }
}
try{
 const binding={artifactRoot:root,mysqlSeries:'8.4',database:'electronic_register_staging',definerUser:'er_stage_definer',definerHost:'%',apiUser:'er_api',apiHost:'172.30.252.%',reviewUser:'er_review',reviewHost:'172.30.252.%'};
 const input=path.join(temp,'input.json'),bundle=path.join(temp,'bundle');fs.writeFileSync(input,JSON.stringify(binding));
 cp.execFileSync(process.execPath,[path.join(root,'tools/prepare-release-sql.cjs'),input,bundle]);
 const manifest=JSON.parse(fs.readFileSync(path.join(root,'migrations/release/manifest.json'))),derived=JSON.parse(fs.readFileSync(path.join(bundle,'binding.json')));
 assert.equal(derived.mysqlSeries,'8.4');assert.deepStrictEqual(derived.records.map(r=>r.source),manifest.requiredMigrations);
 for(const record of derived.records){
  assert.equal(record.sourceHash,manifest.files[record.source]);
  const sql=fs.readFileSync(path.join(bundle,record.artifact),'utf8');
  assert.equal(record.artifactHash,crypto.createHash('sha256').update(sql).digest('hex').toUpperCase());
  assert(!sql.includes('NO_AUTO_CREATE_USER'));assert(!sql.includes('mysql.proc'));
 }
 const grants=fs.readFileSync(path.join(bundle,'00-principals.sql'),'utf8');assert(grants.includes('GRANT SHOW_ROUTINE'));assert(!grants.includes('mysql.proc'));
 assert(!grants.includes('GRANT ALL'));assert(!grants.includes('SUPER'));
}finally{fs.rmSync(temp,{recursive:true,force:true});}
console.log(`MySQL 8.4 adapter and negative controls passed; ${fks} standard FK; historical contract unchanged; runtime NOT tested`);
