// Versioned compatibility rules. Historical 5.7 artifacts remain byte-for-byte intact.
const mode=s=>s.split(',').filter(v=>v!=='NO_AUTO_CREATE_USER').join(',');
const column=s=>s.replace(/\b(tinyint|smallint|mediumint|int|bigint)\((\d+)\)/gi,(all,type,width)=>type.toLowerCase()==='tinyint'&&width==='1'?all:type);
// Derive input/expected SQL only; never rewrite observed server collations.
function legacyScopeCollation(source){
 return source.replace(/(CREATE TABLE `mt3a_legacy_scope` \([^;]*?\) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4)(?=;|\r?\n|$)/g,'$1 COLLATE=utf8mb4_general_ci');
}
function tableDdl(name,source){
 return ddl(name==='mt3a_legacy_scope'?legacyScopeCollation(source).replace(/^  `scope` enum\('legacy'\) NOT NULL,$/m,"  `scope` enum('legacy') COLLATE utf8mb4_general_ci NOT NULL,"):source);
}
// Expected header only: quote identifiers and derive the 8.4 ROW/BEGIN separator.
// MySQL prints the table/FOR boundary as one space too. Preserve body bytes.
// Never apply this derivation to observed server DDL.
function triggerDdl(source){
 const identifier='(?:`(?:``|[^`])+`|[A-Za-z0-9_$]+)';
 const header=new RegExp('^(CREATE (?:DEFINER=`[^`]+`@`[^`]+` )?TRIGGER\\s+)('+identifier+')(\\s+(?:BEFORE|AFTER)\\s+(?:INSERT|UPDATE|DELETE)\\s+ON\\s+)('+identifier+')(?=\\s+FOR\\s+EACH\\s+ROW\\b)');
 if(!header.test(source))throw Error('Unsupported historical trigger header');
 const quote=name=>name.startsWith('`')?name:'`'+name+'`';
 const match=header.exec(source);
 const prefix=match[1]+quote(match[2])+match[3]+quote(match[4]);
 return prefix+source.slice(match[0].length).replace(/^\s+(?=FOR EACH ROW\b)/,' ').replace(/^( FOR EACH ROW)\r?\n(?=BEGIN\b)/,'$1 ');
}
function sql(source){
 // ROW_NUMBER is reserved in 8.x. Qualified NEW.row_number stays legal/unchanged.
 return legacyScopeCollation(source).replaceAll(',NO_AUTO_CREATE_USER','').replace(/(?<![.\w`])row_number(?![\w`])/g,'`row_number`');
}
function ddl(source){
 // MySQL 8 removes integer display widths and prints explicit default collations.
 // Preserve every column, index, FK, nullability, default and nondefault collation.
 // sql/sql_show.cc (mysql-8.4.13) prints the charset for explicit collations.
 // Match only a column type prefix, never text inside defaults or comments.
 return column(source).replace(/(^  `[^`]+` (?:(?:char|varchar)\(\d+\)|enum\('[^']*'(?:,'[^']*')*\)|tinytext|text|mediumtext|longtext)) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci/gm,'$1 COLLATE utf8mb4_unicode_ci').replace(/ CHARACTER SET ascii COLLATE ascii_general_ci/g,' CHARACTER SET ascii');
}
// Expected additive DDL only. Source hashes are checked by the caller first.
// Match complete FK identities against versioned CREATE TABLEs, never live DDL.
function additiveTables(tables,sources,foreignKeys){
 const declarations=new Map();
 const canonical=s=>s.replaceAll('`','').replace(/\s+/g,'');
 for(const source of sources)for(const table of source.matchAll(/CREATE TABLE `?(\w+)`? \(([\s\S]*?)\) ENGINE=[^;]+;/g)){
  if(declarations.has(table[1]))throw Error('Duplicate additive table: '+table[1]);
  declarations.set(table[1],[...table[2].matchAll(/CONSTRAINT\s+`?(\w+)`?\s+FOREIGN KEY\s*\(([^)]+)\)\s+REFERENCES\s+(`?\w+`?)\s*\(([^)]+)\)\s+ON DELETE (RESTRICT)(?=\s*(?:,|$))/gm)]);
  const count=[...table[2].matchAll(/\bFOREIGN KEY\b/g)].length;
  if(declarations.get(table[1]).length!==count)throw Error('Unsupported additive FK declaration: '+table[1]);
 }
 if(JSON.stringify([...declarations.keys()].sort())!==JSON.stringify(Object.keys(tables).sort()))throw Error('Additive source table inventory mismatch');
 // The complete declaration pattern above accepts explicit DELETE RESTRICT
 // and *omitted* ON UPDATE only. In 8.4 the latter is reported as NO ACTION.
 // Do not normalize live metadata or equate explicit RESTRICT with NO ACTION.
 if(foreignKeys){
  const remaining=new Map([...declarations].flatMap(([table,rows])=>rows.map(m=>[table+'.'+m[1],m])));
  for(const fk of foreignKeys){
   const key=fk.TABLE_NAME+'.'+fk.CONSTRAINT_NAME,source=remaining.get(key);
   if(!source||canonical(source[3])!==fk.REFERENCED_TABLE_NAME||fk.UPDATE_RULE!=='RESTRICT'||fk.DELETE_RULE!==source[5])throw Error('Additive FK metadata source mismatch: '+key);
   remaining.delete(key);
   fk.UPDATE_RULE='NO ACTION';
  }
  if(remaining.size)throw Error('Additive FK metadata inventory mismatch');
 }
 return Object.fromEntries(Object.entries(tables).map(([name,sql])=>{
  const remaining=new Map(declarations.get(name).map(m=>[m[1],m]));
  const derived=sql.replace(/CONSTRAINT `([^`]+)` FOREIGN KEY \(([^)]+)\) REFERENCES (`[^`]+`) \(([^)]+)\)(?=,?\r?$)/gm,(text,key,local,target,remote)=>{
   const source=remaining.get(key);
   if(!source||canonical(local)!==canonical(source[2])||canonical(target)!==canonical(source[3])||canonical(remote)!==canonical(source[4]))throw Error('Additive FK source mismatch: '+name+'.'+key);
   remaining.delete(key);
   return text+' ON DELETE '+source[5];
  });
  if(remaining.size||[...derived.matchAll(/\bFOREIGN KEY\b/g)].length!==declarations.get(name).length)throw Error('Additive FK inventory mismatch: '+name);
  return [name,derived];
 }));
}
function contract(value,sources){
 const copy=JSON.parse(JSON.stringify(value));
 copy.tables=additiveTables(copy.tables,sources,copy.foreignKeys);
 for(const t of copy.triggers)t.SQL_MODE=mode(t.SQL_MODE);
 for(const c of copy.columns){
  c.COLUMN_TYPE=column(c.COLUMN_TYPE);
  if(/^CURRENT_TIMESTAMP(?:\(\d*\))?$/i.test(c.COLUMN_DEFAULT||''))c.EXTRA='DEFAULT_GENERATED'+(c.EXTRA?' '+c.EXTRA:'');
 }
 return copy;
}
module.exports={mode,column,sql,ddl,tableDdl,triggerDdl,contract};
