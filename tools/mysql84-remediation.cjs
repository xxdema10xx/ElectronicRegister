// Candidate derivation only. Never connects to a database or edits historical SQL.
'use strict';
const fs=require('fs'),path=require('path'),crypto=require('crypto'),cp=require('child_process');
const root=path.resolve(__dirname,'..');
const digest=s=>crypto.createHash('sha256').update(s.replaceAll('\r\n','\n')).digest('hex').toUpperCase();
const binaryValue=String.raw`(?:BINARY\s+[\w.]+|_binary\s*'[^']*'|CAST\([^)]*\bAS\s+BINARY(?:\(\d+\))?\)|CONVERT\([^)]*\bUSING\s+BINARY\))`;
const forbidden=new RegExp(String.raw`\b${binaryValue}\s+(?:NOT\s+)?(?:REGEXP|RLIKE)\b|\b(?:REGEXP|RLIKE)\s+(?:BINARY\b|${binaryValue})|\bREGEXP_LIKE\s*\(\s*${binaryValue}`,'i');
function derive(source){
 // ICU $ may match before a final line terminator. SQL decodes \\ to \;
 // \z anchors the absolute end. All certified trigger modes decode backslashes.
 const expression=(value,pattern)=>`REGEXP_LIKE(CONVERT(${value} USING utf8mb4) COLLATE utf8mb4_bin, ${pattern.replace(/\$'$/, "\\\\z'")}, 'c')`;
 return source.replace(/\bBINARY\s+([\w.]+)\s+REGEXP\s+('[^']*')/g,(_,v,p)=>expression(v,p))
  .replace(/\b([\w.]+)\s+NOT\s+REGEXP\s+BINARY\s+('[^']*')/g,(_,v,p)=>'NOT '+expression(v,p));
}
function candidate(){
 const historical=JSON.parse(fs.readFileSync(path.join(root,'migrations/release/manifest.json'),'utf8'));
 const file='migrations/bootstrap/001_schema.sql',source=fs.readFileSync(path.join(root,file),'utf8');
 if(historical.release!=='er-quality-1'||digest(source)!==historical.files[file])throw Error('Historical source drift');
 const contract=JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json'),'utf8'));
 const triggers=Object.entries(contract.triggers).filter(([,t])=>forbidden.test(t.statement)).map(([name,t])=>{
  const statement=derive(t.statement);
  if(forbidden.test(statement)||statement===t.statement)throw Error('Uncovered binary regex: '+name);
  const declaration=new RegExp('\\bTRIGGER\\s+`?'+name+'`?\\s').exec(source);
  if(!declaration)throw Error('Missing source declaration: '+name);
  return {name,source:file,line:source.slice(0,declaration.index).split('\n').length,
   beforeHash:digest(t.statement),afterHash:digest(statement),mode:t.mode,charset:t.charset,collation:t.collation,
   databaseCollation:t.database_collation,statement};
 });
 if(triggers.length!==19)throw Error('Review changed trigger inventory');
 const sql=require('./mysql84-contract.cjs').sql(derive(source));
 if(forbidden.test(sql))throw Error('Forbidden executable SQL');
 return {format:1,release:'er-quality-2-mysql84',status:'IMPLEMENTED_PENDING_RUNTIME',
  predecessor:historical.release,predecessorManifestHash:digest(fs.readFileSync(path.join(root,'migrations/release/manifest.json'),'utf8')),
  mysql:JSON.parse(fs.readFileSync(path.join(root,'infra/staging/mysql-image.json'),'utf8')),triggers,bootstrapHash:digest(sql)};
}
function inventory(){
 const files=cp.execFileSync('git',['ls-files','-z'],{cwd:root,encoding:'utf8'}).split('\0').filter(f=>/\.(sql|cs|cjs|py|json|ya?ml|ps1|csproj)$/.test(f)&&!/(?:TestResults|evidence|node_modules|packages.lock)/i.test(f));
 const categories={regex:/\b(?:REGEXP|RLIKE|REGEXP_LIKE)\b/i,binary:/\bBINARY\b|\bCOLLATE\b/i,
  removed:/NO_AUTO_CREATE_USER|mysql_native_password|mysql\.proc|\bPASSWORD\s*\(|SQL_CALC_FOUND_ROWS|utf8mb3/i,
  metadata:/INFORMATION_SCHEMA/i,locking:/FOR UPDATE|GET_LOCK|RELEASE_LOCK/i,
  provider:/MySqlConnector|Pomelo|ServerVersion/i};
 return {files,hits:files.flatMap(file=>fs.readFileSync(path.join(root,file),'utf8').split('\n').flatMap((text,i)=>{
  const kinds=Object.entries(categories).filter(([,r])=>r.test(text)).map(([k])=>k);
  return kinds.length?[{file,line:i+1,categories:kinds,text:text.trim().slice(0,240),lineHash:digest(text)}]:[];
 }))};
}
if(require.main===module){
 const result=candidate();
 if(process.argv[2]==='--write'){
  const out=path.join(root,'migrations/mysql84-remediation');
  fs.mkdirSync(out,{recursive:true});
  fs.writeFileSync(path.join(out,'candidate.json'),JSON.stringify(result,null,2)+'\n');
  fs.writeFileSync(path.join(out,'inventory.json'),JSON.stringify(inventory(),null,2)+'\n');
 }else{
  const recorded=JSON.parse(fs.readFileSync(path.join(root,'migrations/mysql84-remediation/candidate.json'),'utf8'));
  if(JSON.stringify(recorded)!==JSON.stringify(result))throw Error('Candidate drift');
 }
 console.log(`${result.triggers.length} candidate trigger corrections; historical release unchanged; deployment BLOCKED`);
}
module.exports={derive,candidate,inventory,forbidden};
