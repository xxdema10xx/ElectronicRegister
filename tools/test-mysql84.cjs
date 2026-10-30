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
  assert.throws(()=>vm.runInNewContext(fs.readFileSync(file,'utf8'),context),e=>e===stop);
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
