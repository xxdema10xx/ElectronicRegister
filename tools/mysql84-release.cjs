'use strict';
// New release closure. Never rewrites er-quality-1 or historical migrations.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),cp=require('child_process');
const root=path.resolve(__dirname,'..'),dir='migrations/mysql84-remediation';
const old=require('../migrations/release/manifest.json'),adapter=require('./mysql84-contract.cjs');
const {derive,candidate,forbidden}=require('./mysql84-remediation.cjs');
const sha=s=>crypto.createHash('sha256').update(s.toString().replaceAll('\r\n','\n')).digest('hex').toUpperCase();
function contract(modern=true){
 const base=structuredClone(require('../migrations/bootstrap/schema-contract.json'));
 const add=require('./release-additive-contract.cjs').forMySql84();
 const triggers=Object.fromEntries(Object.entries(base.triggers).map(([name,t])=>{
  const m=base.trigger_metadata[name];
  return [name,{statement:adapter.triggerDdl(modern?derive(t.statement):t.statement).replace(/^CREATE DEFINER=`[^`]+`@`[^`]+` /,'CREATE '),
   mode:adapter.mode(t.mode),charset:t.charset,collation:t.collation,database_collation:t.database_collation,
   table:m.EVENT_OBJECT_TABLE,event:m.EVENT_MANIPULATION,timing:m.ACTION_TIMING,order:m.ACTION_ORDER}];
 }));
 for(const t of add.contract.triggers)triggers[t.TRIGGER_NAME]={body:t.ACTION_STATEMENT,mode:t.SQL_MODE,
  charset:'utf8mb4',collation:'utf8mb4_unicode_ci',database_collation:'utf8mb4_unicode_ci',
  table:t.EVENT_OBJECT_TABLE,event:t.EVENT_MANIPULATION,timing:t.ACTION_TIMING,order:1};
 const tables=Object.fromEntries([...Object.entries(base.tables).map(([n,s])=>[n,adapter.tableDdl(n,s)]),
  ...Object.entries(add.contract.tables).map(([n,s])=>[n,adapter.ddl(s)])]);
 const foreignKeys=[],keyColumns=[];
 for(const [name,ddl] of Object.entries(base.tables))for(const m of ddl.matchAll(/CONSTRAINT `([^`]+)` FOREIGN KEY \(([^)]+)\) REFERENCES `([^`]+)` \(([^)]+)\)([^\n]*)/g)){
  const columns=s=>s.replaceAll('`','').split(',').map(v=>v.trim());
  const local=columns(m[2]),remote=columns(m[4]);
  if(local.length!==remote.length)throw Error('Foreign-key arity drift');
  foreignKeys.push({TABLE_NAME:name,CONSTRAINT_NAME:m[1],REFERENCED_TABLE_NAME:m[3],UPDATE_RULE:/ON UPDATE (NO ACTION|RESTRICT|CASCADE|SET NULL)/.exec(m[5])?.[1]||'NO ACTION',DELETE_RULE:/ON DELETE (NO ACTION|RESTRICT|CASCADE|SET NULL)/.exec(m[5])?.[1]||'NO ACTION'});
  local.forEach((column,i)=>keyColumns.push({TABLE_NAME:name,CONSTRAINT_NAME:m[1],COLUMN_NAME:column,ORDINAL_POSITION:i+1,REFERENCED_TABLE_NAME:m[3],REFERENCED_COLUMN_NAME:remote[i]}));
 }
 if(foreignKeys.length!==base.inventory.fks)throw Error('Incomplete base FK contract');
 const metadata=Object.fromEntries(['columns','indexes','foreignKeys','keyColumns'].map(k=>[k,structuredClone(add.contract[k])]));
 metadata.foreignKeys.push(...foreignKeys);metadata.keyColumns.push(...keyColumns);
 return {release:modern?'er-quality-2-mysql84':'er-quality-1',tables,triggers,routine:{body:add.body,mode:add.contract.triggers[0].SQL_MODE},
  metadata};
}
// Git is the authority for publishable inputs, including new addable source files.
const localArtifact=/(^|\/)(?:\.staff-private|\.formative-private|[^/]*-private|private|bin|obj|node_modules|__pycache__|\.git)(?:\/|$)/;
function publishableFiles(){
 return new Set(cp.execFileSync('git',['ls-files','--cached','--others','--exclude-standard','-z'],
  {cwd:root,encoding:'utf8',maxBuffer:32*1024*1024}).split('\0').filter(f=>f&&!localArtifact.test(f)));
}
function records(){
 const publishable=publishableFiles();
 const extra=['tools/mysql84-remediation.cjs','tools/test-mysql84-remediation.cjs','tools/mysql84-release.cjs',
  'tools/mysql84_release.py','tools/test_mysql84_release.py','tools/mysql84_runtime.py','tools/historical_artifacts.py','tools/test-mysql84-release.cjs',
  '.github/workflows/mysql84-remediation-candidate.yml','.github/workflows/verify.yml','.github/actionlint.yaml',dir+'/candidate.json',dir+'/contract.json',
  'tests/Integration/IntegrationFixture.cs','tests/Integration/Mysql84ReleaseTests.cs','tests/Integration/Mysql84ReleaseHttp.cs','infra/release.mysql84.example.json'];
 const bootstrap=Object.keys(require('../migrations/bootstrap/manifest.json').files);
 const calendar=require('../tests/Integration/INSTITUTION_CALENDAR_VERIFICATION.json').artifacts.map(a=>a.path);
 const roots=['Backend/ElectronicRegisterAPI/src','tests/Unit','tests/Integration','tools','infra','migrations/mt1','migrations/mt3','migrations/identity-account'];
 const active=[...publishable].filter(f=>roots.some(r=>f.startsWith(r+'/'))&&!f.split('/').includes('TestResults'))
  .filter(f=>/\.(cs|csproj|cjs|py|ps1|sql|json|config|ya?ml|sh|conf)$/.test(f)&&(!f.includes('appsettings')||f.endsWith('appsettings.example.json')));
 return Object.fromEntries([...new Set([...Object.keys(old.files),...bootstrap,...calendar,...extra,...active])].filter(f=>publishable.has(f)&&fs.existsSync(path.join(root,f))).sort().map(f=>[f,sha(fs.readFileSync(path.join(root,f)))]));
}
function manifest(){const publishable=publishableFiles();return {format:1,release:'er-quality-2-mysql84',predecessor:'er-quality-1',
 predecessorRevision:'8e7d9ecd42c001f1fa9b30e120535ce1bb0ef6e8',predecessorManifestHash:sha(fs.readFileSync(path.join(root,'migrations/release/manifest.json'))),
 mysql:candidate().mysql,requiredMigrations:old.requiredMigrations,changedTriggers:candidate().triggers.map(t=>t.name),
 retiredArtifacts:[...new Set([...Object.keys(require('../migrations/bootstrap/manifest.json').files),...require('../tests/Integration/INSTITUTION_CALENDAR_VERIFICATION.json').artifacts.map(a=>a.path)])].filter(f=>!publishable.has(f)||!fs.existsSync(path.join(root,f))).sort(),files:records()};}
function verify(){
 const actual=manifest(),saved=JSON.parse(fs.readFileSync(path.join(root,dir,'manifest.json')));
 if(JSON.stringify(actual)!==JSON.stringify(saved))throw Error('New release closure drift');
 if(JSON.stringify(contract())!==JSON.stringify(JSON.parse(fs.readFileSync(path.join(root,dir,'contract.json')))))throw Error('Contract drift');
 const revision=actual.predecessorRevision;
 for(const [f,h] of Object.entries(old.files)){
  const bytes=cp.execFileSync('git',['show',revision+':'+f],{cwd:root,maxBuffer:16*1024*1024});
  if(sha(bytes)!==h)throw Error('Historical closure drift: '+f);
 }
 const current=contract();
 for(const [n,t] of Object.entries(current.triggers))if(forbidden.test(t.statement||t.body))throw Error('Binary regex in new trigger '+n);
 const embedded=fs.readFileSync(path.join(root,'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/Mt5cOnboardingIntegrity.cs'),'utf8');
 // This exact 5.7-only fallback is preserved, never executed by the 8.4 branch.
 const legacy=`: "token_hash NOT REGEXP BINARY '^[A-F0-9]{64}$' OR channel_hash NOT REGEXP BINARY '^[A-F0-9]{64}$'";`;
 if(!embedded.includes(legacy)||forbidden.test(embedded.replace(legacy,''))||!embedded.includes('REGEXP_LIKE'))throw Error('Embedded SQL not remediated');
 return actual;
}
function bundle(config,out,modern=true){
 if(fs.existsSync(out))throw Error('NEW output directory required');
 // The immutable generator validates all historical SQL before adapting it.
 const temp=path.join(path.dirname(out),'.mysql84-binding-'+crypto.randomUUID()+'.json');
 fs.writeFileSync(temp,JSON.stringify({...config,mysqlSeries:'8.4',artifactRoot:root}));
 try{cp.execFileSync(process.execPath,[path.join(root,'tools/prepare-release-sql.cjs'),temp,out]);}finally{fs.unlinkSync(temp);}
 const binding=JSON.parse(fs.readFileSync(path.join(out,'binding.json')));
 if(modern){
  for(const r of binding.records){
   const file=path.join(out,r.artifact),sql=derive(fs.readFileSync(file,'utf8'));
   if(forbidden.test(sql))throw Error('Forbidden bundle SQL');
   fs.writeFileSync(file,sql);r.artifactHash=sha(sql);
  }
  fs.writeFileSync(path.join(out,'98-new-release.sql'),`USE \`${config.database}\`;\nINSERT INTO er_schema_release(version) VALUES('er-quality-2-mysql84');\n`);
  binding.release='er-quality-2-mysql84';
 }
 binding.files=Object.fromEntries(fs.readdirSync(out).filter(f=>f.endsWith('.sql')).sort().map(f=>[f,sha(fs.readFileSync(path.join(out,f)))]));
 fs.writeFileSync(path.join(out,'binding.json'),JSON.stringify(binding,null,2)+'\n');
}
if(require.main===module){
 const action=process.argv[2];
 if(action==='--write'){
  fs.writeFileSync(path.join(root,dir,'contract.json'),JSON.stringify(contract(),null,2)+'\n');
  fs.writeFileSync(path.join(root,dir,'manifest.json'),JSON.stringify(manifest(),null,2)+'\n');
 }else if(action==='contract')process.stdout.write(JSON.stringify(contract(process.argv[3]!=='old')));
 else if(action==='bundle'){verify();bundle(JSON.parse(fs.readFileSync(process.argv[3])),path.resolve(process.argv[4]),process.argv[5]!=='old');}
 else {const m=verify();console.log(m.release+': new closure and immutable predecessor verified; runtime certification separate');}
}
module.exports={contract,manifest,verify,bundle,sha};
