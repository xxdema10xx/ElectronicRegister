// Versioned compatibility rules. Historical 5.7 artifacts remain byte-for-byte intact.
const mode=s=>s.split(',').filter(v=>v!=='NO_AUTO_CREATE_USER').join(',');
const column=s=>s.replace(/\b(tinyint|smallint|mediumint|int|bigint)\((\d+)\)/gi,(all,type,width)=>type.toLowerCase()==='tinyint'&&width==='1'?all:type);
function sql(source){
 // ROW_NUMBER is reserved in 8.x. Qualified NEW.row_number stays legal/unchanged.
 return source.replaceAll(',NO_AUTO_CREATE_USER','').replace(/(?<![.\w`])row_number(?![\w`])/g,'`row_number`');
}
function ddl(source){
 // MySQL 8 removes integer display widths and prints explicit default collations.
 // Preserve every column, index, FK, nullability, default and nondefault collation.
 return column(source).replace(/ CHARACTER SET ascii COLLATE ascii_general_ci/g,' CHARACTER SET ascii');
}
function contract(value){
 const copy=JSON.parse(JSON.stringify(value));
 for(const t of copy.triggers)t.SQL_MODE=mode(t.SQL_MODE);
 for(const c of copy.columns){
  c.COLUMN_TYPE=column(c.COLUMN_TYPE);
  if(/^CURRENT_TIMESTAMP(?:\(\d*\))?$/i.test(c.COLUMN_DEFAULT||''))c.EXTRA='DEFAULT_GENERATED'+(c.EXTRA?' '+c.EXTRA:'');
 }
 return copy;
}
module.exports={mode,column,sql,ddl,contract};
