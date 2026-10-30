// Read-only schema preflight. No DDL, DML, retries, migration or deployment.
const fs=require('fs'),path=require('path'),cp=require('child_process');
const crypto=require('crypto');
const audit=process.argv.includes('--audit'),mismatches=[];
let check={id:'configuration',category:'configuration',object:'release'};
const identify=(category,object,id)=>{check={id,category,object};};
const hash=s=>crypto.createHash('sha256').update(s).digest('hex').toUpperCase();
function equal(actual,expected,difference){
 if(actual!==expected){check={...check,difference,expectedHash:hash(expected),actualHash:hash(actual)};if(audit){mismatches.push({...check});return;}throw Error('Preflight mismatch');}
}
try{
const root=path.resolve(__dirname,'..'),config=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,''));
const isolated=process.argv.includes('--isolated-test');
if(audit){
 const state=JSON.parse(fs.readFileSync('/state/public/staging.json','utf8'));
 if(isolated||! /^[a-f0-9]{32}$/.test(state.id)||state.format!==3||state.mysql_version!=='8.4.13'||config.host!=='er-stage-db'||config.port!==3306||config.database!=='electronic_register_staging'||config.user!=='er_review'||config.expectedVersion!=='8.4.13'||config.expectedDataDir!=='/var/lib/mysql'||!config.caFile||!config.schemaDefiner)throw Error('Isolated Docker staging required');
 config.auditStageId=state.id;
}
if(!isolated&&(!config.caFile||!config.passwordFile||config.user==='root'))throw Error('Release preflight requires explicit CA and non-root review identity');
if((!isolated||config.schemaDefiner)&&!/^[A-Za-z0-9_.%-]+@[A-Za-z0-9_.%-]+$/.test(config.schemaDefiner||''))throw Error('Explicit release schema definer required');
if(isolated&&(!path.resolve(config.expectedDataDir).startsWith(path.join(root,'.staff-private','instance-'))||config.host!=='127.0.0.1'||config.database!=='electronic_register_test'||[3306,13306].includes(config.port)))throw Error('Unattested isolated target');
const password=fs.readFileSync(config.passwordFile,'utf8');
const args=['--no-defaults','--protocol=TCP','--host='+config.host,'--port='+config.port,'--database='+config.database,'--user='+config.user,'--batch','--raw','--skip-column-names','--default-character-set=utf8mb4',...isolated?['--ssl-mode=DISABLED']:['--ssl-mode=VERIFY_IDENTITY','--tls-version=TLSv1.2','--ssl-ca='+config.caFile]];
let queryNumber=0;
function read(sql){
 if(audit&&!/^(SELECT|SHOW)\b/i.test(sql))throw Error('Audit reads only');
 if(!/^(SELECT|SHOW|CALL er_schema_ready\(\))/i.test(sql))throw Error('Read-only statements only');
 const step='preflight-query-'+(++queryNumber);
 const p=cp.spawnSync(config.mysqlClient||'mysql',args,{input:sql,encoding:'utf8',env:{...process.env,MYSQL_PWD:password},maxBuffer:10*1024*1024});
 if(p.status!==0){
  const match=/^ERROR (\d{1,5}) \(([A-Z0-9]{5})\)(?: at line (\d{1,9}))?:/m.exec(p.stderr||'');
  if(!audit)console.error('ER_SQL_FAILURE '+JSON.stringify({step,exitCode:p.status,mysqlErrno:match?+match[1]:null,sqlstate:match?match[2]:null,line:match&&match[3]?+match[3]:null}));
  if(audit){equal('query-failed','query-succeeded','query');return 'null';}throw {sqlFailure:true};
 }
 const output=p.stdout.trim();
 return output==='NULL'?'null':output;
}
identify('identity','database','database-identity');
const [version,port,datadir,database]=read("SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE();").split('\t');
if(version!==config.expectedVersion||+port!==config.port||database!==config.database||datadir.toLowerCase().replace(/\/$/,'')!==config.expectedDataDir.replaceAll('\\','/').toLowerCase().replace(/\/$/,''))throw Error('Release database identity mismatch');
if(audit&&read('SELECT id FROM er_stage_control.identity')!==config.auditStageId)throw Error('Ownership mismatch');
identify('contract','release','contract-load');
const contract=JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json'),'utf8'));
const mysql84=/^8\.4\./.test(version),adapter=require('./mysql84-contract.cjs');
const additive=mysql84?require('./release-additive-contract.cjs').forMySql84():require('./release-additive-contract.cjs');
const normalize=s=>s.replaceAll('\r\n','\n');
for(const [table,ddl]of Object.entries(contract.tables)){
 identify('table',table,'base-table-ddl');
 const actual=read('SHOW CREATE TABLE `'+table+'`;').split('\t').slice(1).join('\t').replace(/ AUTO_INCREMENT=\d+/g,'').replaceAll('\r\n','\n');
 equal(mysql84?adapter.ddl(actual):actual,mysql84?adapter.tableDdl(table,ddl):ddl,'ddl');
}
if(mysql84){
 identify('table','mt3a_legacy_scope','base-table-collation');
 equal(read("SELECT TABLE_COLLATION FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mt3a_legacy_scope'"),'utf8mb4_general_ci','TABLE_COLLATION');
 identify('column','mt3a_legacy_scope.scope','base-column-collation');
 equal(read("SELECT COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mt3a_legacy_scope' AND COLUMN_NAME='scope'"),'utf8mb4_general_ci','COLLATION_NAME');
}
for(const [table,ddl]of Object.entries(additive.contract.tables)){
 identify('table',table,'additive-table-ddl');
 const actual=read('SHOW CREATE TABLE `'+table+'`;').split('\t').slice(1).join('\t');
 equal(normalize(mysql84?adapter.ddl(actual):actual),normalize(mysql84?adapter.ddl(ddl):ddl),'ddl');
}
for(const [name,t]of Object.entries(contract.triggers)){
 identify('trigger',name,'base-trigger-mode');
 let expected=mysql84?adapter.triggerDdl(t.statement):t.statement;
 if(isolated)expected=expected.replaceAll('@@port=13306','@@port='+config.port).replaceAll("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'",`LOWER(REPLACE(@@datadir,CHAR(92),'/'))='${datadir.toLowerCase()}'`);
 const row=read('SHOW CREATE TRIGGER `'+name+'`;').split('\t');
 equal(row[1]||'',mysql84?adapter.mode(t.mode):t.mode,'SQL_MODE');
 identify('trigger',name,'base-trigger-charset');
 for(const [index,value,field] of [[-4,t.charset,'CHARACTER_SET_CLIENT'],[-3,t.collation,'COLLATION_CONNECTION'],[-2,t.database_collation,'DATABASE_COLLATION']])equal(row.at(index)||'',value,field);
 const statement=row.slice(2,-4).join('\t');
 const normalize=s=>s.replace(/^CREATE DEFINER=`[^`]+`@`[^`]+` TRIGGER /,'CREATE TRIGGER ').replaceAll('\r\n','\n');
 identify('trigger',name,'base-trigger-ddl');
 equal(normalize(statement),normalize(expected),'ddl');
}
identify('trigger','inventory','trigger-inventory');
const triggers=JSON.parse(read("SELECT JSON_ARRAYAGG(JSON_OBJECT('name',TRIGGER_NAME,'table',EVENT_OBJECT_TABLE,'event',EVENT_MANIPULATION,'timing',ACTION_TIMING,'body',ACTION_STATEMENT,'mode',SQL_MODE,'definer',DEFINER,'charset',CHARACTER_SET_CLIENT,'collation',COLLATION_CONNECTION,'database_collation',DATABASE_COLLATION)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()"))||[];
for(const expected of additive.contract.triggers){identify('trigger',expected.TRIGGER_NAME,'additive-trigger-metadata');const actual=triggers.find(t=>t.name===expected.TRIGGER_NAME);for(const [field,key] of Object.entries({table:'EVENT_OBJECT_TABLE',event:'EVENT_MANIPULATION',timing:'ACTION_TIMING',mode:'SQL_MODE'}))equal(actual?.[field]||'',expected[key],key);identify('trigger',expected.TRIGGER_NAME,'additive-trigger-ddl');equal(normalize(actual?.body||''),normalize(expected.ACTION_STATEMENT),'body');}
if(mysql84){
 for(const t of additive.contract.triggers){
  identify('trigger',t.TRIGGER_NAME,'additive-trigger-charset');
  const actual=triggers.find(row=>row.name===t.TRIGGER_NAME)||{};
  for(const [key,value] of Object.entries({charset:'utf8mb4',collation:'utf8mb4_unicode_ci',database_collation:'utf8mb4_unicode_ci'}))equal(actual[key]||'',value,key);
 }
}
identify('routine','er_schema_ready','routine-metadata');
const routine=JSON.parse(read("SELECT JSON_OBJECT('body',ROUTINE_DEFINITION,'security',SECURITY_TYPE,'access',SQL_DATA_ACCESS,'definer',DEFINER,'deterministic',IS_DETERMINISTIC,'comment',ROUTINE_COMMENT,'mode',SQL_MODE,'charset',CHARACTER_SET_CLIENT,'collation',COLLATION_CONNECTION,'database_collation',DATABASE_COLLATION) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE() AND ROUTINE_NAME='er_schema_ready' AND ROUTINE_TYPE='PROCEDURE'"))||{};
equal(routine.security||'','DEFINER','SECURITY_TYPE');equal(routine.access||'','READS SQL DATA','SQL_DATA_ACCESS');
if(mysql84){
 for(const [key,value] of Object.entries({deterministic:'NO',comment:'',mode:additive.contract.triggers[0].SQL_MODE,charset:'utf8mb4',collation:'utf8mb4_unicode_ci',database_collation:'utf8mb4_unicode_ci'}))equal(routine[key]||'',value,key);
 identify('routine','er_schema_ready','routine-parameters');
 equal(read("SELECT COUNT(*) FROM information_schema.PARAMETERS WHERE SPECIFIC_SCHEMA=DATABASE() AND SPECIFIC_NAME='er_schema_ready'"),'0','parameters');
}
identify('routine','er_schema_ready','routine-body');
equal(normalize(routine.body||''),additive.body,'body');
if(!isolated||config.schemaDefiner){
 identify('definer','er_schema_ready','routine-definer');
 equal(routine.definer||'',config.schemaDefiner,'DEFINER');
 for(const name of [...Object.keys(contract.triggers),...additive.contract.triggers.map(t=>t.TRIGGER_NAME)]){identify('definer',name,'trigger-definer');equal(triggers.find(t=>t.name===name)?.definer||'',config.schemaDefiner,'DEFINER');}
 identify('definer','schema-owner','definer-account');
 const owner=config.schemaDefiner.split('@');
 equal(read("SELECT COUNT(*) FROM mysql.user WHERE User='"+owner[0]+"' AND Host='"+owner[1]+"'"),'1','account');
}
const inventory=(category,id,actual,expected)=>{identify(category,'inventory',id);equal(JSON.stringify([...actual].sort()),JSON.stringify([...expected].sort()),'inventory');};
inventory('trigger','trigger-inventory',triggers.map(t=>t.name),[...Object.keys(contract.triggers),...additive.contract.triggers.map(t=>t.TRIGGER_NAME)]);
inventory('table','table-inventory',read('SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME').split('\n'),[...Object.keys(contract.tables),...Object.keys(additive.contract.tables)]);
inventory('routine','routine-inventory',read("SELECT CONCAT(ROUTINE_NAME,':',ROUTINE_TYPE) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE() ORDER BY ROUTINE_NAME").split('\n'),['er_schema_ready:PROCEDURE']);
for(const [section,table,filter,keys] of [
 ['columns','COLUMNS','TABLE_SCHEMA=DATABASE()',['TABLE_NAME','COLUMN_NAME','COLUMN_TYPE','IS_NULLABLE','COLUMN_DEFAULT','EXTRA','COLLATION_NAME']],
 ['indexes','STATISTICS','TABLE_SCHEMA=DATABASE()',['TABLE_NAME','INDEX_NAME','SEQ_IN_INDEX','COLUMN_NAME','NON_UNIQUE','SUB_PART','INDEX_TYPE','COLLATION']],
 ['foreignKeys','REFERENTIAL_CONSTRAINTS','CONSTRAINT_SCHEMA=DATABASE()',['TABLE_NAME','CONSTRAINT_NAME','REFERENCED_TABLE_NAME','UPDATE_RULE','DELETE_RULE']],
 ['keyColumns','KEY_COLUMN_USAGE','TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME IS NOT NULL',['TABLE_NAME','CONSTRAINT_NAME','COLUMN_NAME','ORDINAL_POSITION','REFERENCED_TABLE_NAME','REFERENCED_COLUMN_NAME']]
]){
 identify('metadata','inventory','additive-'+section);
 const actual=JSON.parse(read('SELECT JSON_ARRAYAGG(JSON_OBJECT('+keys.map(k=>"'"+k+"',"+k).join(',')+')) FROM information_schema.'+table+' WHERE '+filter))||[];
 const canonical=r=>JSON.stringify(keys.map(k=>r[k]));
 for(const name of Object.keys(additive.contract.tables)){
  identify('metadata',name,'additive-'+section);
  equal(JSON.stringify(actual.filter(r=>r.TABLE_NAME===name).map(canonical).sort()),JSON.stringify(additive.contract[section].filter(r=>r.TABLE_NAME===name).map(canonical).sort()),section);
 }
}
identify('metadata','er_schema_ready','readiness-probe');
// Audit evaluates the versioned SELECT directly: never execute an untrusted routine body.
equal(read(audit?additive.body.slice('BEGIN\n'.length,-'\nEND'.length).trim():'CALL er_schema_ready();'),'1','ready');
if(audit){for(const item of mismatches)console.log(JSON.stringify(item));if(mismatches.length)process.exitCode=1;}
if(!audit)console.log(JSON.stringify({ReadOnly:true,IdentityVerified:true,BaseTablesVerified:Object.keys(contract.tables).length,BaseTriggersVerified:Object.keys(contract.triggers).length,AdditiveTablesVerified:Object.keys(additive.contract.tables).length,AdditiveTriggersVerified:additive.contract.triggers.length,RoutineDefinitionVerified:true,DefinerVerified:!isolated||!!config.schemaDefiner,AdditiveProbeReady:true,Release:'er-quality-1'}));

}catch(error){
 if(audit){for(const item of mismatches)console.log(JSON.stringify(item));console.log(JSON.stringify({...check,difference:'unavailable',expectedHash:hash('verified'),actualHash:hash('unavailable')}));}
 else if(!error.sqlFailure)console.error('ER_PREFLIGHT_FAILURE '+JSON.stringify(check));
 process.exitCode=1;
}
