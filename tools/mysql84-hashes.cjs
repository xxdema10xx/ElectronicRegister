'use strict';
// Candidate-only storage contract. VARCHAR(65) reserves an invalid extra byte
// even when non-strict assignment truncates input; CHECK rejects that sentinel.
const names=new Set(['token_hash','channel_hash','intent_hash','nonce','request_hash','file_sha256','preview_digest']);
function columns(tables){
 return Object.fromEntries(Object.entries(tables).flatMap(([table,ddl])=>{
  const fields=[...ddl.matchAll(/^  `([^`]+)` char\(64\) CHARACTER SET ascii[^\n]*$/gm)].filter(m=>names.has(m[1])).map(m=>({name:m[1],definition:m[0].trim().replace(/,$/,'').replace('char(64)','varchar(65)')})).sort((a,b)=>a.name.localeCompare(b.name));
  return fields.length?[[table,fields]]:[];
 }));
}
const checkName=(table,column)=>'ck_er84_'+table+'_'+column;
const clause=column=>`((\`${column}\` is null) or ((length(\`${column}\`) = 64) and (unhex(\`${column}\`) is not null) and (cast(\`${column}\` as char charset binary) = cast(hex(unhex(\`${column}\`)) as char charset binary))))`;
function tableDdl(table,ddl,fields){
 ddl=ddl.replaceAll('\r\n','\n');
 if(!fields)return ddl;
 for(const field of fields)ddl=ddl.replace(new RegExp('(^  `'+field.name+'` )char\\(64\\)','m'),'$1varchar(65)');
 return ddl.replace(/\n\) ENGINE=/,',\n'+fields.map(f=>'  CONSTRAINT `'+checkName(table,f.name)+'` CHECK ('+clause(f.name)+')').join(',\n')+'\n) ENGINE=');
}
function alter(table,fields){return 'ALTER TABLE `'+table+'` '+fields.flatMap(f=>['MODIFY COLUMN '+f.definition,'ADD CONSTRAINT `'+checkName(table,f.name)+'` CHECK ('+clause(f.name)+')']).join(', ');}
function adaptSql(sql,inventory){
 return sql.replace(/(CREATE TABLE\s+`?(\w+)`?\s*\()([\s\S]*?)(\) ENGINE=[^;]+;)/g,(all,head,table,body,tail)=>{
  const fields=inventory[table];if(!fields)return all;
  for(const f of fields)body=body.replace(new RegExp('(^\\s*`?'+f.name+'`?\\s+)char\\(64\\)','gmi'),'$1varchar(65)');
  return head+body.replace(/\s*$/,'')+',\n'+fields.map(f=>' CONSTRAINT `'+checkName(table,f.name)+'` CHECK ('+clause(f.name)+')').join(',\n')+'\n'+tail;
 });
}
module.exports={columns,checkName,clause,tableDdl,alter,adaptSql};
