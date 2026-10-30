// Deterministic, reversible DDL faults ONLY on this task's identity-attested DB.
const fs=require('fs'),cp=require('child_process'),{execute,sql,root,run,i}=require('./sql.cjs');
const {contract,body}=require(root+'/tools/release-additive-contract.cjs');
const config=run+'/gate-preflight.json';if(!fs.existsSync(config))fs.writeFileSync(config,JSON.stringify({host:'127.0.0.1',port:i.Port,database:'electronic_register_test',user:'root',passwordFile:run+'/password.txt',expectedVersion:'5.7.24',expectedDataDir:run+'/data',mysqlClient:'C:/MAMP/bin/mysql/bin/mysql.exe'}),{flag:'wx'});
const records=[];
function preflight(){const p=cp.spawnSync('node',['tools/release-preflight.cjs',config,'--isolated-test'],{cwd:root,encoding:'utf8'});return {exit:p.status,error:p.stderr.trim().split('\n').find(s=>s.startsWith('Error:'))||'',output:p.stdout.trim()};}
function ready(){const p=sql('CALL er_schema_ready();');return p.exit===0&&p.stdout==='1';}
function positive(){if(!ready())throw Error('Complete schema is not ready');const p=preflight();if(p.exit)throw Error('Positive preflight failed: '+p.error);}
function fault(name,apply,restore,options={}){
 execute(apply);
 try{const accepted=ready(),p=preflight();records.push({name,readinessAccepted:accepted,preflightExit:p.exit});if(accepted||p.exit===0)throw Error('Fault accepted: '+name);
  if(options.http){const p=cp.spawnSync('node',[__dirname+'/readiness-process.cjs','503','lease-until-missing-'+(process.env.GATE_ATTEMPT||'1'),'er_gate_api'],{cwd:root,encoding:'utf8'});if(p.status)throw Error('Real Program did not reject missing column');}
 }finally{execute(restore);if(!ready())throw Error('Restoration failed: '+name);}
}
positive();
execute("CREATE USER IF NOT EXISTS 'er_gate_api'@'localhost'; GRANT SELECT,INSERT,UPDATE,DELETE ON electronic_register_test.* TO 'er_gate_api'@'localhost'; GRANT EXECUTE ON PROCEDURE electronic_register_test.er_schema_ready TO 'er_gate_api'@'localhost';");
const denied=sql('CREATE TABLE er_gate_forbidden(id int)',{user:'er_gate_api',password:''});if(!denied.exit)throw Error('API unexpectedly has DDL');
const apiReady=sql('CALL er_schema_ready();',{user:'er_gate_api',password:''});if(apiReady.exit||apiReady.stdout!=='1')throw Error('Least privilege API cannot probe');
const live=cp.spawnSync('node',[__dirname+'/readiness-process.cjs','200','least-privilege-complete-'+(process.env.GATE_ATTEMPT||'1'),'er_gate_api'],{cwd:root,encoding:'utf8'});if(live.status)throw Error('Least privilege API HTTP failed');
fault('original: lease_until absent','ALTER TABLE identity_password_attempt DROP COLUMN lease_until','ALTER TABLE identity_password_attempt ADD COLUMN lease_until datetime(6) DEFAULT NULL AFTER lease_id',{http:true});
for(const c of contract.columns){const table='`'+c.TABLE_NAME+'`',col='`'+c.COLUMN_NAME+'`';
 const value=c.COLUMN_TYPE.startsWith('datetime')?"'2001-01-01 00:00:00'":/^(bigint|int)/.test(c.COLUMN_TYPE)?'7':"'gate'";
 const altered=c.COLUMN_TYPE.startsWith('char(36)')?"'00000000-0000-0000-0000-000000000000'":value;
 const original=c.COLUMN_DEFAULT===null?(c.IS_NULLABLE==='YES'?'SET DEFAULT NULL':'DROP DEFAULT'):'SET DEFAULT '+(c.COLUMN_DEFAULT==='CURRENT_TIMESTAMP(6)'?c.COLUMN_DEFAULT:"'"+String(c.COLUMN_DEFAULT).replaceAll("'","''")+"'");
 const restore=c.COLUMN_DEFAULT==='CURRENT_TIMESTAMP(6)'?`ALTER TABLE ${table} MODIFY ${col} ${c.COLUMN_TYPE} ${c.IS_NULLABLE==='NO'?'NOT NULL':'NULL'} DEFAULT CURRENT_TIMESTAMP(6)`:`ALTER TABLE ${table} ALTER COLUMN ${col} ${original}`;
 fault('column default: '+c.TABLE_NAME+'.'+c.COLUMN_NAME,`ALTER TABLE ${table} ALTER COLUMN ${col} SET DEFAULT ${altered}`,restore);
}
fault('column type: lease_until','ALTER TABLE identity_password_attempt MODIFY lease_until datetime(3) DEFAULT NULL','ALTER TABLE identity_password_attempt MODIFY lease_until datetime(6) DEFAULT NULL');
fault('column nullability: attempts','ALTER TABLE identity_password_attempt MODIFY attempts int NULL','ALTER TABLE identity_password_attempt MODIFY attempts int NOT NULL');
const indexes=new Map();for(const idx of contract.indexes){const k=idx.TABLE_NAME+'.'+idx.INDEX_NAME;if(!indexes.has(k))indexes.set(k,[]);indexes.get(k).push(idx);}
for(const [key,parts]of indexes){const t='`'+parts[0].TABLE_NAME+'`',name=parts[0].INDEX_NAME;
 if(name==='PRIMARY'){
  const cols=parts.map(p=>'`'+p.COLUMN_NAME+'`').join(',');execute(`ALTER TABLE ${t} ADD INDEX er_gate_backup_key(${cols})`);
  try{fault('primary key: '+key,`ALTER TABLE ${t} DROP PRIMARY KEY`,`ALTER TABLE ${t} ADD PRIMARY KEY(${cols})`);}finally{execute(`ALTER TABLE ${t} DROP INDEX er_gate_backup_key`);}
 }else fault('index: '+key,`ALTER TABLE ${t} RENAME INDEX \`${name}\` TO er_gate_renamed`,`ALTER TABLE ${t} RENAME INDEX er_gate_renamed TO \`${name}\``);
}
for(const fk of contract.foreignKeys){const columns=contract.keyColumns.filter(c=>c.TABLE_NAME===fk.TABLE_NAME&&c.CONSTRAINT_NAME===fk.CONSTRAINT_NAME);const names=key=>columns.map(c=>'`'+c[key]+'`').join(',');
 fault('foreign key: '+fk.CONSTRAINT_NAME,`ALTER TABLE \`${fk.TABLE_NAME}\` DROP FOREIGN KEY \`${fk.CONSTRAINT_NAME}\``,`ALTER TABLE \`${fk.TABLE_NAME}\` ADD CONSTRAINT \`${fk.CONSTRAINT_NAME}\` FOREIGN KEY(${names('COLUMN_NAME')}) REFERENCES \`${fk.REFERENCED_TABLE_NAME}\`(${names('REFERENCED_COLUMN_NAME')}) ON DELETE ${fk.DELETE_RULE} ON UPDATE ${fk.UPDATE_RULE}`);
}
for(const t of contract.triggers){const create=`SET SESSION sql_mode='${t.SQL_MODE}';\nDELIMITER $$\nCREATE TRIGGER \`${t.TRIGGER_NAME}\` ${t.ACTION_TIMING} ${t.EVENT_MANIPULATION} ON \`${t.EVENT_OBJECT_TABLE}\` FOR EACH ROW ${t.ACTION_STATEMENT}$$\nDELIMITER ;`;
 fault('trigger missing: '+t.TRIGGER_NAME,`DROP TRIGGER \`${t.TRIGGER_NAME}\``,create);
 fault('trigger body: '+t.TRIGGER_NAME,`DROP TRIGGER \`${t.TRIGGER_NAME}\`;\nDELIMITER $$\nCREATE TRIGGER \`${t.TRIGGER_NAME}\` ${t.ACTION_TIMING} ${t.EVENT_MANIPULATION} ON \`${t.EVENT_OBJECT_TABLE}\` FOR EACH ROW BEGIN SET @er_gate_negative=1; END$$\nDELIMITER ;`,`DROP TRIGGER \`${t.TRIGGER_NAME}\`;\n`+create);
}
fault('table: password quota','RENAME TABLE identity_password_attempt TO er_gate_quota_backup','RENAME TABLE er_gate_quota_backup TO identity_password_attempt');
for(const marker of ['er-quality-1',contract.marker])fault('marker: '+marker,`DELETE FROM er_schema_release WHERE version='${marker}'`,`INSERT INTO er_schema_release(version) VALUES('${marker}')`);
fault('routine SQL SECURITY','ALTER PROCEDURE er_schema_ready SQL SECURITY INVOKER','ALTER PROCEDURE er_schema_ready SQL SECURITY DEFINER');
fault('routine absent','DROP PROCEDURE er_schema_ready',`DELIMITER $$\nCREATE PROCEDURE er_schema_ready() SQL SECURITY DEFINER READS SQL DATA ${body}$$\nDELIMITER ;`);
// DROP PROCEDURE removes routine-specific grants in MySQL 5.7.
execute("GRANT EXECUTE ON PROCEDURE electronic_register_test.er_schema_ready TO 'er_gate_api'@'localhost'; REVOKE EXECUTE ON PROCEDURE electronic_register_test.er_schema_ready FROM 'er_gate_api'@'localhost'");
const noExecute=sql('CALL er_schema_ready();',{user:'er_gate_api',password:''});
execute("GRANT EXECUTE ON PROCEDURE electronic_register_test.er_schema_ready TO 'er_gate_api'@'localhost'");
if(!noExecute.exit)throw Error('No-EXECUTE account passed');positive();
fs.writeFileSync(__dirname+'/schema-matrix.json',JSON.stringify({mysql:'5.7.24',port:i.Port,positivePreflight:true,apiDdlDenied:denied.exit!==0,apiExecuteOnlyProbe:true,noExecuteRejected:noExecute.exit!==0,cases:records},null,2));
console.log(`${records.length} real SQL negative controls rejected; complete schema restored; API has no DDL`);
