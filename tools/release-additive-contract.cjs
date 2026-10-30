// Reviewed 5.7 metadata contract. Verification never learns expected values from a live database.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..');
function render(mysql84=false){
let contract=JSON.parse(fs.readFileSync(path.join(root,'migrations/release/additive-contract.json'),'utf8'));
const base=JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json'),'utf8'));
const sha=s=>crypto.createHash('sha256').update(s.replaceAll('\r\n','\n')).digest('hex').toUpperCase();
for(const [file,hash] of Object.entries(contract.sources))if(sha(fs.readFileSync(path.join(root,file),'utf8'))!==hash)throw Error('Reviewed additive source drift: '+file);
if(mysql84)contract=require('./mysql84-contract.cjs').contract(contract,Object.keys(contract.sources).map(file=>fs.readFileSync(path.join(root,file),'utf8')));
if(contract.format!==1||contract.release!=='er-quality-1'||contract.marker!=='er-quality-1-closure-1')throw Error('Unknown additive contract');
const q=v=>v===null?'NULL':typeof v==='number'?String(v):v===''?"''":"CONVERT(0x"+Buffer.from(String(v),'utf8').toString('hex')+" USING utf8mb4)";
const match=(rows,keys)=>rows.map(row=>'('+keys.map(key=>'`'+key+'` <=> '+q(row[key])).join(' AND ')+')').join(' OR ');
const metadata=(table,where,rows,keys)=>`(SELECT COUNT(*) FROM information_schema.${table} WHERE ${where} AND (${match(rows,keys)}))=${rows.length}`;
const list=names=>names.map(q).join(',');
const tables=[...Object.keys(base.tables),...Object.keys(contract.tables)].sort();
const triggers=[...Object.keys(base.triggers),...contract.triggers.map(t=>t.TRIGGER_NAME)].sort();
const tableSpecs=Object.entries(contract.tables).map(([TABLE_NAME,ddl])=>({TABLE_NAME,TABLE_TYPE:'BASE TABLE',ENGINE:'InnoDB',TABLE_COLLATION:/COLLATE=(\w+)/.exec(ddl)?.[1]||'utf8mb4_general_ci'}));
const checks=[
 `EXISTS(SELECT 1 FROM er_schema_release WHERE version='er-quality-1')`,
 `EXISTS(SELECT 1 FROM er_schema_release WHERE version='${contract.marker}')`,
 `(SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE' AND TABLE_NAME IN (${list(tables)}))=${tables.length}`,
 `(SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN (${list(triggers)}))=${triggers.length}`,
 metadata('TABLES','TABLE_SCHEMA=DATABASE()',tableSpecs,['TABLE_NAME','TABLE_TYPE','ENGINE','TABLE_COLLATION']),
 metadata('COLUMNS','TABLE_SCHEMA=DATABASE()',contract.columns,['TABLE_NAME','COLUMN_NAME','COLUMN_TYPE','IS_NULLABLE','COLUMN_DEFAULT','EXTRA','COLLATION_NAME']),
 metadata('STATISTICS','TABLE_SCHEMA=DATABASE()',contract.indexes,['TABLE_NAME','INDEX_NAME','SEQ_IN_INDEX','COLUMN_NAME','NON_UNIQUE','SUB_PART','INDEX_TYPE','COLLATION']),
 metadata('REFERENTIAL_CONSTRAINTS','CONSTRAINT_SCHEMA=DATABASE()',contract.foreignKeys,['TABLE_NAME','CONSTRAINT_NAME','REFERENCED_TABLE_NAME','UPDATE_RULE','DELETE_RULE']),
 metadata('KEY_COLUMN_USAGE','TABLE_SCHEMA=DATABASE()',contract.keyColumns,['TABLE_NAME','CONSTRAINT_NAME','COLUMN_NAME','ORDINAL_POSITION','REFERENCED_TABLE_NAME','REFERENCED_COLUMN_NAME']),
 `(SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND (${contract.triggers.map(t=>`(${match([t],['TRIGGER_NAME','EVENT_MANIPULATION','EVENT_OBJECT_TABLE','ACTION_TIMING','SQL_MODE'])} AND UPPER(SHA2(REPLACE(ACTION_STATEMENT,CHAR(13),''),256))='${sha(t.ACTION_STATEMENT)}')`).join(' OR ')}))=${contract.triggers.length}`,
 `(SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE() AND ROUTINE_NAME='er_schema_ready' AND ROUTINE_TYPE='PROCEDURE' AND SECURITY_TYPE='DEFINER' AND SQL_DATA_ACCESS='READS SQL DATA')=1`
];
const body='BEGIN\n SELECT (\n '+checks.join('\n AND ')+'\n ) AS ready;\nEND';
const sql=`-- Additive gate repair. 001..004 and the historical bootstrap remain immutable.
-- Apply once through the separate migration identity; no HTTP probe applies DDL.
DROP PROCEDURE er_schema_ready;
DELIMITER $$
CREATE PROCEDURE er_schema_ready()
SQL SECURITY DEFINER
READS SQL DATA
${body}$$
DELIMITER ;
INSERT INTO er_schema_release(version) VALUES('${contract.marker}');
`;
return {contract,body,sql};
}
const {contract,body,sql}=render();
module.exports={contract,body,sql,forMySql84:()=>render(true)};
if(require.main===module){const file=path.join(root,'migrations/identity-account/005_release_gate.sql');if(process.argv.includes('--write'))fs.writeFileSync(file,sql);else if(fs.readFileSync(file,'utf8').replaceAll('\r\n','\n')!==sql)throw Error('Additive gate definition drift');console.log(`Additive gate: ${contract.columns.length} columns, ${contract.indexes.length} index parts, ${contract.foreignKeys.length} FK, ${contract.triggers.length} trigger bodies`);}
