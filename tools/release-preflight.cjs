// Read-only schema preflight. No DDL, DML, retries, migration or deployment.
const fs=require('fs'),path=require('path'),cp=require('child_process');
const root=path.resolve(__dirname,'..'),config=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,''));
const isolated=process.argv.includes('--isolated-test');
if(!isolated&&(!config.caFile||!config.passwordFile||config.user==='root'))throw Error('Release preflight requires explicit CA and non-root review identity');
if((!isolated||config.schemaDefiner)&&!/^[A-Za-z0-9_.%-]+@[A-Za-z0-9_.%-]+$/.test(config.schemaDefiner||''))throw Error('Explicit release schema definer required');
if(isolated&&(!path.resolve(config.expectedDataDir).startsWith(path.join(root,'.staff-private','instance-'))||config.host!=='127.0.0.1'||config.database!=='electronic_register_test'||[3306,13306].includes(config.port)))throw Error('Unattested isolated target');
const password=fs.readFileSync(config.passwordFile,'utf8');
const args=['--no-defaults','--protocol=TCP','--host='+config.host,'--port='+config.port,'--database='+config.database,'--user='+config.user,'--batch','--raw','--skip-column-names','--default-character-set=utf8mb4',...isolated?['--ssl-mode=DISABLED']:['--ssl-mode=VERIFY_IDENTITY','--tls-version=TLSv1.2','--ssl-ca='+config.caFile]];
let queryNumber=0;
function read(sql){
 if(!/^(SELECT|SHOW|CALL er_schema_ready\(\))/i.test(sql))throw Error('Read-only statements only');
 const step='preflight-query-'+(++queryNumber);
 const p=cp.spawnSync(config.mysqlClient||'mysql',args,{input:sql,encoding:'utf8',env:{...process.env,MYSQL_PWD:password},maxBuffer:10*1024*1024});
 if(p.status!==0){
  const match=/^ERROR (\d{1,5}) \(([A-Z0-9]{5})\)(?: at line (\d{1,9}))?:/m.exec(p.stderr||'');
  console.error('ER_SQL_FAILURE '+JSON.stringify({step,exitCode:p.status,mysqlErrno:match?+match[1]:null,sqlstate:match?match[2]:null,line:match&&match[3]?+match[3]:null}));
  process.exit(1);
 }
 return p.stdout.trim();
}
const [version,port,datadir,database]=read("SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/'),DATABASE();").split('\t');
if(version!==config.expectedVersion||+port!==config.port||database!==config.database||datadir.toLowerCase().replace(/\/$/,'')!==config.expectedDataDir.replaceAll('\\','/').toLowerCase().replace(/\/$/,''))throw Error('Release database identity mismatch');
const contract=JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json'),'utf8'));
const mysql84=/^8\.4\./.test(version),adapter=require('./mysql84-contract.cjs');
const additive=mysql84?require('./release-additive-contract.cjs').forMySql84():require('./release-additive-contract.cjs');
const normalize=s=>s.replaceAll('\r\n','\n');
for(const [table,ddl]of Object.entries(contract.tables)){
 const actual=read('SHOW CREATE TABLE `'+table+'`;').split('\t').slice(1).join('\t').replace(/ AUTO_INCREMENT=\d+/g,'').replaceAll('\r\n','\n');
 if((mysql84?adapter.ddl(actual):actual)!==(mysql84?adapter.ddl(ddl):ddl))throw Error('Base schema definition mismatch: '+table);
}
for(const [table,ddl]of Object.entries(additive.contract.tables)){
 const actual=read('SHOW CREATE TABLE `'+table+'`;').split('\t').slice(1).join('\t');
 if(normalize(mysql84?adapter.ddl(actual):actual)!==normalize(mysql84?adapter.ddl(ddl):ddl))throw Error('Additive schema definition mismatch: '+table);
}
for(const [name,t]of Object.entries(contract.triggers)){
 let expected=t.statement;
 if(isolated)expected=expected.replaceAll('@@port=13306','@@port='+config.port).replaceAll("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'",`LOWER(REPLACE(@@datadir,CHAR(92),'/'))='${datadir.toLowerCase()}'`);
 const row=read('SHOW CREATE TRIGGER `'+name+'`;').split('\t');
 if(row[1]!== (mysql84?adapter.mode(t.mode):t.mode))throw Error('Base trigger SQL mode mismatch: '+name);
 if(row.at(-4)!==t.charset||row.at(-3)!==t.collation||row.at(-2)!==t.database_collation)throw Error('Base trigger charset/collation mismatch: '+name);
 const statement=row.slice(2,-4).join('\t');
 const normalize=s=>s.replace(/^CREATE DEFINER=`[^`]+`@`[^`]+` TRIGGER /,'CREATE TRIGGER ').replaceAll('\r\n','\n');
 if(normalize(statement)!==normalize(expected))throw Error('Base trigger definition mismatch: '+name);
}
const triggers=JSON.parse(read("SELECT JSON_ARRAYAGG(JSON_OBJECT('name',TRIGGER_NAME,'table',EVENT_OBJECT_TABLE,'event',EVENT_MANIPULATION,'timing',ACTION_TIMING,'body',ACTION_STATEMENT,'mode',SQL_MODE,'definer',DEFINER)) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()"));
for(const expected of additive.contract.triggers){const actual=triggers.find(t=>t.name===expected.TRIGGER_NAME);if(!actual||actual.table!==expected.EVENT_OBJECT_TABLE||actual.event!==expected.EVENT_MANIPULATION||actual.timing!==expected.ACTION_TIMING||normalize(actual.body)!==normalize(expected.ACTION_STATEMENT)||actual.mode!==expected.SQL_MODE)throw Error('Additive trigger mismatch: '+expected.TRIGGER_NAME);}
const routine=JSON.parse(read("SELECT JSON_OBJECT('body',ROUTINE_DEFINITION,'security',SECURITY_TYPE,'access',SQL_DATA_ACCESS,'definer',DEFINER) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE() AND ROUTINE_NAME='er_schema_ready' AND ROUTINE_TYPE='PROCEDURE'"));
if(!routine||normalize(routine.body||'')!==additive.body||routine.security!=='DEFINER'||routine.access!=='READS SQL DATA')throw Error('Readiness routine definition mismatch');
if(!isolated||config.schemaDefiner){
 if(routine.definer!==config.schemaDefiner||triggers.some(t=>t.definer!==config.schemaDefiner))throw Error('Release objects have unexpected definers');
 const owner=config.schemaDefiner.split('@');
 if(read("SELECT COUNT(*) FROM mysql.user WHERE User='"+owner[0]+"' AND Host='"+owner[1]+"'")!=='1')throw Error('Release definer missing');
}
if(read('CALL er_schema_ready();')!=='1')throw Error('Additive schema prerequisites missing');
console.log(JSON.stringify({ReadOnly:true,IdentityVerified:true,BaseTablesVerified:Object.keys(contract.tables).length,BaseTriggersVerified:Object.keys(contract.triggers).length,AdditiveTablesVerified:Object.keys(additive.contract.tables).length,AdditiveTriggersVerified:additive.contract.triggers.length,RoutineDefinitionVerified:true,DefinerVerified:!isolated||!!config.schemaDefiner,AdditiveProbeReady:true,Release:'er-quality-1'}));
