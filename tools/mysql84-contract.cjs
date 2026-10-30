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
// Expected header only: preserve quoted identifiers, whitespace, DEFINER and body.
// Never apply this derivation to observed server DDL.
function triggerDdl(source){
 const identifier='(?:`(?:``|[^`])+`|[A-Za-z0-9_$]+)';
 const header=new RegExp('^(CREATE (?:DEFINER=`[^`]+`@`[^`]+` )?TRIGGER\\s+)('+identifier+')(\\s+(?:BEFORE|AFTER)\\s+(?:INSERT|UPDATE|DELETE)\\s+ON\\s+)('+identifier+')(?=\\s+FOR\\s+EACH\\s+ROW\\b)');
 if(!header.test(source))throw Error('Unsupported historical trigger header');
 const quote=name=>name.startsWith('`')?name:'`'+name+'`';
 return source.replace(header,(_,prefix,name,middle,table)=>prefix+quote(name)+middle+quote(table));
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
function additiveTables(tables,sources){
 const declarations=new Map();
 const canonical=s=>s.replaceAll('`','').replace(/\s+/g,'');
 for(const source of sources)for(const table of source.matchAll(/CREATE TABLE `?(\w+)`? \(([\s\S]*?)\) ENGINE=[^;]+;/g)){
  if(declarations.has(table[1]))throw Error('Duplicate additive table: '+table[1]);
  declarations.set(table[1],[...table[2].matchAll(/CONSTRAINT\s+`?(\w+)`?\s+FOREIGN KEY\s*\(([^)]+)\)\s+REFERENCES\s+(`?\w+`?)\s*\(([^)]+)\)\s+ON DELETE (RESTRICT)(?=\s*(?:,|$))/gm)]);
  const count=[...table[2].matchAll(/\bFOREIGN KEY\b/g)].length;
  if(declarations.get(table[1]).length!==count)throw Error('Unsupported additive FK declaration: '+table[1]);
 }
 if(JSON.stringify([...declarations.keys()].sort())!==JSON.stringify(Object.keys(tables).sort()))throw Error('Additive source table inventory mismatch');
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
 copy.tables=additiveTables(copy.tables,sources);
 for(const t of copy.triggers)t.SQL_MODE=mode(t.SQL_MODE);
 for(const c of copy.columns){
  c.COLUMN_TYPE=column(c.COLUMN_TYPE);
  if(/^CURRENT_TIMESTAMP(?:\(\d*\))?$/i.test(c.COLUMN_DEFAULT||''))c.EXTRA='DEFAULT_GENERATED'+(c.EXTRA?' '+c.EXTRA:'');
 }
 return copy;
}
module.exports={mode,column,sql,ddl,tableDdl,triggerDdl,contract};
