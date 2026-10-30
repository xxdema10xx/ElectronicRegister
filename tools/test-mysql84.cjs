{
// Execute the production preflight against deterministic MySQL responses.
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert');
const root=path.resolve(__dirname,'..'),file=path.join(__dirname,'release-preflight.cjs');
const base=require('../migrations/bootstrap/schema-contract.json'),add=require('./release-additive-contract.cjs').forMySql84(),adapter=require('./mysql84-contract.cjs');
const config={caFile:'ca',passwordFile:'password',user:'review',schemaDefiner:'owner@%',host:'host',port:3306,database:'db',expectedVersion:'8.4.13',expectedDataDir:'/data/'};
const observedLegacy=base.tables.mt3a_legacy_scope.replace("enum('legacy') NOT NULL","enum('legacy') COLLATE utf8mb4_general_ci NOT NULL")+' COLLATE=utf8mb4_general_ci';
const legacyMutations={
 'ddl-column-collation':s=>s.replace('COLLATE utf8mb4_general_ci','COLLATE utf8mb4_0900_ai_ci'),
 'ddl-enum':s=>s.replace("enum('legacy')","enum('legacy','other')"),
 'ddl-fk':s=>s.replace('REFERENCES `institution` (`id`)','REFERENCES `institution` (`other_id`)'),
 'ddl-index':s=>s.replace('UNIQUE KEY `institution_id`','KEY `institution_id`')
};
const additiveNames=Object.keys(add.contract.tables);
const additiveMutations={
 'missing-restrict':s=>s.replace(' ON DELETE RESTRICT',''),
 'delete-rule':s=>s.replace('ON DELETE RESTRICT','ON DELETE CASCADE'),
 'fk-name':s=>s.replace(/CONSTRAINT `([^`]+)`/, 'CONSTRAINT `$1_changed`'),
 'fk-local-columns':s=>s.replace(/FOREIGN KEY \(`[^`]+`/, 'FOREIGN KEY (`changed`'),
 'fk-referenced-columns':s=>s.replace(/REFERENCES (`[^`]+`) \(`[^`]+`/, 'REFERENCES $1 (`changed`')
};
// Independent observed fixture: historical SHOW CREATE plus the seven explicit
// clauses from the migrations. Never use the derived expected tables as input.
const observedAdditive=Object.fromEntries(Object.entries(require('./release-additive-contract.cjs').contract.tables).map(([name,ddl])=>[name,ddl.replace(/(CONSTRAINT .* REFERENCES .*\))(?=,?\r?$)/gm,'$1 ON DELETE RESTRICT')]));
// Independent observed fixture, built by tokenizing only the header.
function observedTrigger(statement){
 const end=statement.search(/\s+FOR\s+EACH\s+ROW\b/);assert(end>0);
 const tokens=statement.slice(0,end).split(/(\s+)/);
 for(const i of [tokens.indexOf('TRIGGER')+2,tokens.indexOf('ON')+2])if(!tokens[i].startsWith('\x60'))tokens[i]='\x60'+tokens[i]+'\x60';
 return tokens.join('')+statement.slice(end).replace(/(FOR EACH ROW)\r?\n(?=BEGIN\b)/,'$1 ');
}
const triggerMutations={
 'trigger-name':s=>s.replace(/(TRIGGER \x60)[^\x60]+/,'$1changed'),
 'trigger-table':s=>s.replace(/( ON \x60)[^\x60]+/,'$1changed'),
 'trigger-event':s=>s.replace(/ (INSERT|UPDATE|DELETE) ON /,(_,v)=>' '+(v==='DELETE'?'INSERT':'DELETE')+' ON '),
 'trigger-timing':s=>s.replace(/ (BEFORE|AFTER) /,(_,v)=>' '+(v==='BEFORE'?'AFTER':'BEFORE')+' '),
 'trigger-body':s=>s+' SQL secret-password'
};
function simulate(mutant,audit=false){
 const output=[];
 let connections=0;
 const currentConfig=audit?{...config,host:'er-stage-db',database:'electronic_register_staging',user:'er_review',expectedDataDir:'/var/lib/mysql'}:config;
 if(mutant?.config)Object.assign(currentConfig,mutant.config);
 function read(sql){
  if(sql===add.body.slice('BEGIN\n'.length,-'\nEND'.length).trim())return '1';
  if(sql.startsWith('SELECT VERSION'))return audit?'8.4.13\t3306\t/var/lib/mysql/\telectronic_register_staging':'8.4.13\t3306\t/data/\tdb';
  if(sql==='SELECT id FROM er_stage_control.identity')return 'a'.repeat(32);
  if(sql.startsWith('SHOW CREATE TABLE')){const name=/`([^`]+)`/.exec(sql)[1];let ddl=name==='mt3a_legacy_scope'?observedLegacy:base.tables[name]||observedAdditive[name];if(name==='mt3a_legacy_scope'&&legacyMutations[mutant])ddl=legacyMutations[mutant](ddl);if(mutant?.table===name)ddl=additiveMutations[mutant.kind](ddl);return name+'\t'+(ddl.replace(/ COLLATE utf8mb4_unicode_ci/g,' CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci')+(mutant==='table'?' SQL secret-password':'')).replace('COLLATE=utf8mb4_general_ci',mutant==='ddl-collation'?'COLLATE=utf8mb4_0900_ai_ci':'COLLATE=utf8mb4_general_ci');}
  if(sql.startsWith('SHOW CREATE TRIGGER')){const name=/`([^`]+)`/.exec(sql)[1],t=base.triggers[name];return [name,mutant==='trigger-mode'?'changed':adapter.mode(t.mode),(triggerMutations[mutant==='multi'?'trigger-body':mutant]?triggerMutations[mutant==='multi'?'trigger-body':mutant](observedTrigger(t.statement)):observedTrigger(t.statement))+(mutant==='trigger'?' SQL secret-password':''),t.charset,mutant==='trigger-collation'?'changed':t.collation,t.database_collation,'timestamp'].join('\t');}
  if(sql.startsWith('SELECT TABLE_COLLATION'))return mutant==='table-collation'?'utf8mb4_0900_ai_ci':'utf8mb4_general_ci';
  if(sql.startsWith('SELECT COLLATION_NAME'))return mutant==='column-collation'?'utf8mb4_0900_ai_ci':'utf8mb4_general_ci';
  if(mutant==='missing-triggers'&&sql.includes('information_schema.TRIGGERS'))return 'NULL';
  if(mutant==='missing-routine'&&sql.includes('information_schema.ROUTINES'))return 'NULL';
  if(sql.includes('information_schema.TRIGGERS'))return JSON.stringify([
   ...Object.keys(base.triggers).map(name=>({name,definer:mutant==='trigger-definer'?'secret-password':config.schemaDefiner})),
   ...add.contract.triggers.map(t=>({name:t.TRIGGER_NAME,table:t.EVENT_OBJECT_TABLE,event:t.EVENT_MANIPULATION,timing:t.ACTION_TIMING,body:t.ACTION_STATEMENT,mode:t.SQL_MODE,charset:'utf8mb4',collation:'utf8mb4_unicode_ci',database_collation:'utf8mb4_unicode_ci',definer:config.schemaDefiner}))]);
  if(sql.startsWith('SELECT TABLE_NAME')&&mutant==='inventory')return 'unexpected';
  if(sql.startsWith('SELECT TABLE_NAME'))return [...Object.keys(base.tables),...Object.keys(add.contract.tables)].sort().join('\n');
  if(sql.startsWith('SELECT CONCAT(ROUTINE_NAME'))return 'er_schema_ready:PROCEDURE';
  for(const [section,table] of Object.entries({columns:'COLUMNS',indexes:'STATISTICS',foreignKeys:'REFERENTIAL_CONSTRAINTS',keyColumns:'KEY_COLUMN_USAGE'}))if(sql.includes('FROM information_schema.'+table+' '))return JSON.stringify(mutant==='metadata'?add.contract[section].slice(1):add.contract[section]);
  if(sql.includes('information_schema.PARAMETERS'))return '0';
  if(sql.includes('information_schema.ROUTINES'))return JSON.stringify({deterministic:'NO',comment:'',mode:add.contract.triggers[0].SQL_MODE,charset:'utf8mb4',collation:'utf8mb4_unicode_ci',database_collation:'utf8mb4_unicode_ci',body:add.body+(['routine','multi'].includes(mutant)?' SQL secret-password':''),security:'DEFINER',access:'READS SQL DATA',definer:mutant==='definer'?'secret-password':config.schemaDefiner});
  return '1';
 }
 const context={__dirname,require:id=>id==='fs'?{readFileSync:(f,...args)=>f==='config'?JSON.stringify(currentConfig):f==='/state/public/staging.json'?JSON.stringify({id:'a'.repeat(32),format:3,mysql_version:'8.4.13'}):f==='password'?'secret-password':fs.readFileSync(f,...args)}:id==='child_process'?{spawnSync:(_cmd,_args,options)=>{connections++;assert(/^(SELECT|SHOW|CALL er_schema_ready\(\))/i.test(options.input));assert(_args.includes('--ssl-mode=VERIFY_IDENTITY'));if(audit)assert(/^(SELECT|SHOW)\b/i.test(options.input));if(audit&&mutant==='query'&&options.input.startsWith('SHOW CREATE TRIGGER'))return {status:1,stderr:'secret-password SQL'};return {status:0,stdout:read(options.input)};}}:require(id),console:{error:s=>output.push(s),log:s=>{if(audit)output.push(s);}},process:{argv:['node',file,'config',...(audit?['--audit']:[])],env:{}}};
 vm.runInNewContext(fs.readFileSync(file,'utf8'),context);
 if(audit){if(mutant?.config)assert.equal(connections,0);assert.equal(context.process.exitCode,mutant?1:undefined);const records=output.map(JSON.parse);for(const row of records){assert.deepEqual(Object.keys(row).sort(),['actualHash','category','difference','expectedHash','id','object']);assert.match(row.actualHash,/^[A-F0-9]{64}$/);assert.match(row.expectedHash,/^[A-F0-9]{64}$/);}assert(!output.join('').includes('secret-password'));return records;}
 if(!mutant){assert.equal(context.process.exitCode,undefined);assert.equal(output.length,0);return;}
 assert.equal(context.process.exitCode,1);assert.equal(output.length,1);assert(!output[0].includes('secret-password'));assert(!output[0].includes('CREATE '));
 return JSON.parse(output[0].replace('ER_PREFLIGHT_FAILURE ',''));
}
simulate();
simulate(null,true);
const missingTriggers=simulate('missing-triggers',true);assert(missingTriggers.some(r=>r.id==='trigger-inventory'));assert.equal(missingTriggers.filter(r=>r.id==='trigger-definer').length,180);
const missingRoutine=simulate('missing-routine',true);assert(missingRoutine.some(r=>r.id==='routine-body'));assert(missingRoutine.some(r=>r.id==='routine-definer'));
assert(simulate('inventory',true).some(r=>r.id==='table-inventory'));
const metadata=simulate('metadata',true);for(const section of ['columns','indexes','foreignKeys','keyColumns'])assert(metadata.some(r=>r.id==='additive-'+section));
for(const config of [{user:'root'},{caFile:null},{host:'existing-db'},{database:'production'},{expectedVersion:'8.4.12'},{expectedDataDir:'/existing'}])simulate({config},true);
const multiple=simulate('multi',true);assert.equal(multiple.filter(r=>r.id==='base-trigger-ddl').length,174);assert(multiple.some(r=>r.id==='routine-body'));
const queries=simulate('query',true);assert.equal(queries.filter(r=>r.difference==='query').length,174);assert(queries.some(r=>r.id==='base-trigger-ddl'));
for(const mutant of [...Object.keys(triggerMutations),'trigger-mode','trigger-collation','trigger-definer'])assert(simulate(mutant,true).length>0);
assert.equal(simulate('trigger-mode').id,'base-trigger-mode');assert.equal(simulate('trigger-collation').id,'base-trigger-charset');
for(const mutant of Object.keys(triggerMutations))assert.equal(simulate(mutant).id,'base-trigger-ddl');
for(const table of additiveNames){
 assert.equal(adapter.ddl(add.contract.tables[table]),adapter.ddl(observedAdditive[table]),table);
 if(table==='er_schema_release')continue;
 for(const kind of Object.keys(additiveMutations)){
  const failure=simulate({table,kind});assert.equal(failure.id,'additive-table-ddl');assert.equal(failure.object,table);assert.notEqual(failure.actualHash,failure.expectedHash);
 }
}
for(const mutant of Object.keys(legacyMutations)){
 const failure=simulate(mutant);assert.equal(failure.id,'base-table-ddl');assert.equal(failure.object,'mt3a_legacy_scope');assert.notEqual(failure.actualHash,failure.expectedHash);
}
for(const [mutant,id,object] of [['ddl-collation','base-table-ddl','mt3a_legacy_scope'],['table-collation','base-table-collation','mt3a_legacy_scope'],['column-collation','base-column-collation','mt3a_legacy_scope.scope']]){
 const failure=simulate(mutant);assert.equal(failure.id,id);assert.equal(failure.object,object);assert.notEqual(failure.actualHash,failure.expectedHash);
}
const failures=['table','trigger','routine','definer','trigger-definer'].map(value=>simulate(value));
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
for(const key of ['indexes','foreignKeys','keyColumns'])assert.deepStrictEqual(modern.contract[key],old.contract[key]);
const restrictCounts={identity_account_state:1,identity_account_event:1,institution_profile_acquisition:4,identity_password_attempt:1,er_schema_release:0};
const sources=Object.keys(old.contract.sources).map(file=>fs.readFileSync(path.join(root,file),'utf8'));
for(const [name,count] of Object.entries(restrictCounts)){
 assert.equal((modern.contract.tables[name].match(/ON DELETE RESTRICT/g)||[]).length,count,name);
 assert.equal(modern.contract.tables[name].replaceAll(' ON DELETE RESTRICT',''),old.contract.tables[name]);
 console.log(`Offline additive DDL verified: ${name}; ${count} explicit DELETE RESTRICT`);
}
assert.equal(crypto.createHash('sha256').update(adapter.ddl(modern.contract.tables.identity_account_state).replaceAll('\r\n','\n')).digest('hex').toUpperCase(),'65964642BF1665C58D0DCB96508D9555F53FB356E2A742CEAFDECD628D8F1DAC');
for(const change of [s=>s.replace('ON DELETE RESTRICT',''),s=>s.replace('ON DELETE RESTRICT','ON DELETE CASCADE'),s=>s.replace('fk_identity_state_user','fk_changed'),s=>s.replace('FOREIGN KEY(user_id)','FOREIGN KEY(display_name)'),s=>s.replace('REFERENCES `user`(id)','REFERENCES `user`(other_id)')])assert.throws(()=>adapter.contract(old.contract,sources.map(change)));
assert.throws(()=>adapter.contract(old.contract,sources.slice(1)));
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
const snapshot=JSON.stringify(base);
let bareTriggers=0,bareTables=0,quotedTables=0,newlines=0;
for(const [name,t] of Object.entries(base.triggers)){
 const header=/^(CREATE (?:DEFINER=\x60[^\x60]+\x60@\x60[^\x60]+\x60 )?TRIGGER\s+)(\x60[^\x60]+\x60|\w+)(\s+(?:BEFORE|AFTER)\s+(?:INSERT|UPDATE|DELETE)\s+ON\s+)(\x60[^\x60]+\x60|\w+)(\s+FOR\s+EACH\s+ROW\b)/.exec(t.statement);
 assert(header,name);bareTriggers+=!header[2].startsWith('\x60');bareTables+=!header[4].startsWith('\x60');quotedTables+=header[4].startsWith('\x60');newlines+=header[5].includes('\n');
 const derived=adapter.triggerDdl(t.statement),quote=v=>v.startsWith('\x60')?v:'\x60'+v+'\x60';
 assert.equal(derived,(header[1]+quote(header[2])+header[3]+quote(header[4])+t.statement.slice(header[0].length-header[5].length)).replace(/(FOR EACH ROW)\r?\n(?=BEGIN\b)/,'$1 '),name);
 assert.equal(adapter.triggerDdl(derived),derived,name);
 for(const space of [' ','\n','\r\n','\t'])assert.equal(adapter.triggerDdl(t.statement.replace(/\s+FOR\s+EACH\s+ROW\b/,space+'FOR EACH ROW')),derived.replace(/\s+FOR\s+EACH\s+ROW\b/,space+'FOR EACH ROW'),name);
}
assert.equal(Object.keys(base.triggers).length,174);assert.deepEqual([bareTriggers,bareTables,quotedTables],[174,136,38]);assert(newlines>0);
assert.equal(JSON.stringify(base),snapshot);
const bodyText="CREATE TRIGGER sample BEFORE INSERT ON sample FOR EACH ROW SET @text='FOR EACH ROW\nBEGIN';";
assert.equal(adapter.triggerDdl(bodyText),bodyText.replace('TRIGGER sample','TRIGGER `sample`').replace('ON sample','ON `sample`'));
for(const t of Object.values(base.triggers)){const tail=t.statement.slice(t.statement.indexOf('BEGIN'));if(t.statement.includes('BEGIN'))assert(adapter.triggerDdl(t.statement).endsWith(tail));}
const canonical=s=>s.replace(/^CREATE DEFINER=\x60[^\x60]+\x60@\x60[^\x60]+\x60 TRIGGER /,'CREATE TRIGGER ').replaceAll('\r\n','\n');
assert.equal(Object.values(base.triggers).filter(t=>/FOR EACH ROW\r?\nBEGIN\b/.test(t.statement)).length,21);
const sha=s=>crypto.createHash('sha256').update(canonical(s)).digest('hex').toUpperCase();
assert.equal(sha(adapter.triggerDdl(base.triggers.calendar_assignment_binding_bu.statement)),'5039D672316C43F43964EDA8185A6D74785CFB8E7B922FE84DAB617F0928AF4D');
assert.equal(sha(base.triggers.administration_event_delete.statement),'FF99581A508BAA2951538D997BE5F529718CAF427EC9232886DF33BE41E31FFE');
assert.equal(sha(adapter.triggerDdl(base.triggers.administration_event_delete.statement)),'5AD2E0FF381EF67E25CDDFCBBFD1E0BB837FBBEB4FE9B986FAD094A53E23DEDF');
console.log('Offline: 174 historical headers verified, including '+newlines+' with newline before FOR; incident SHA256 reproduced');
const legacy=base.tables.mt3a_legacy_scope;
const derivedLegacy=legacy+' COLLATE=utf8mb4_general_ci';
const expectedLegacy=derivedLegacy.replace("enum('legacy') NOT NULL","enum('legacy') COLLATE utf8mb4_general_ci NOT NULL");
assert.equal(adapter.tableDdl('mt3a_legacy_scope',legacy),expectedLegacy);
assert.equal(crypto.createHash('sha256').update(adapter.tableDdl('mt3a_legacy_scope',legacy).replaceAll('\r\n','\n')).digest('hex').toUpperCase(),'4861D496E8CAE58C19EDA216A1DDF05D5BAA2F623D98945376CB3083C28B1B2D');
assert.equal(adapter.sql(legacy+';'),derivedLegacy+';');
assert.equal(adapter.sql(derivedLegacy+';'),derivedLegacy+';');
assert.equal(adapter.sql(legacy+' COLLATE=utf8mb4_0900_ai_ci;'),legacy+' COLLATE=utf8mb4_0900_ai_ci;');
for(const [name,ddl] of Object.entries(base.tables).filter(([name])=>name!=='mt3a_legacy_scope'))assert.equal(adapter.tableDdl(name,ddl),adapter.ddl(ddl));
assert.notEqual(adapter.ddl(legacy+' COLLATE=utf8mb4_0900_ai_ci'),adapter.tableDdl('mt3a_legacy_scope',legacy));
assert.deepStrictEqual(Object.entries(base.tables).filter(([,d])=>!d.slice(d.lastIndexOf('ENGINE=')).includes('COLLATE=')).map(([n])=>n),['mt3a_legacy_scope']);
for(const [name,ddl] of Object.entries(old.contract.tables)){
 assert(ddl.endsWith('ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci'));
 assert.equal(adapter.tableDdl(name,ddl),adapter.ddl(ddl));
 assert.equal(adapter.sql(ddl),ddl);
}
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
  if(record.source==='migrations/bootstrap/001_schema.sql'){const historical=fs.readFileSync(path.join(root,record.source),'utf8').match(/CREATE TABLE `mt3a_legacy_scope` \([\s\S]*?;/)[0];assert(sql.includes(historical.replace(/;$/,' COLLATE=utf8mb4_general_ci;')));assert(!sql.includes(historical));}
  assert(!sql.includes('NO_AUTO_CREATE_USER'));assert(!sql.includes('mysql.proc'));
 }
 const grants=fs.readFileSync(path.join(bundle,'00-principals.sql'),'utf8');assert(grants.includes('GRANT SHOW_ROUTINE'));assert(!grants.includes('mysql.proc'));
 assert(!grants.includes('GRANT ALL'));assert(!grants.includes('SUPER'));
}finally{fs.rmSync(temp,{recursive:true,force:true});}
console.log(`MySQL 8.4 adapter and negative controls passed; ${fks} standard FK; historical contract unchanged; runtime NOT tested`);
