// Canonical release closure. Text SHA256 normalizes CRLF to LF; historical SQL
// raw-byte hashes and historical manifests remain unchanged.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),cp=require('child_process');
const root=path.resolve(__dirname,'..'),manifestPath=path.join(root,'migrations/release/manifest.json');
const roots=['Backend/ElectronicRegisterAPI/src','migrations/mt1','migrations/mt3','migrations/identity-account','infra'];
const explicit=['infra/staging/Dockerfile.ops','tools/StagingSupport/StagingSupport.csproj','tools/StagingSupport/Program.cs','tools/StagingSupport/packages.lock.json','tools/test_staging.py','.github/workflows/staging-package.yml','tools/ReleaseTransportProbe/ReleaseTransportProbe.csproj','tools/ReleaseTransportProbe/Program.cs','tools/ReleaseTransportProbe/packages.lock.json','infra/nginx.release.conf','tools/prepare-release-sql.cjs','tools/test-release-manifest.cjs','migrations/release/additive-contract.json','tools/release-additive-contract.cjs','migrations/bootstrap/001_schema.sql','migrations/bootstrap/schema-contract.json','migrations/bootstrap/manifest.json','infra/Dockerfile','Directory.Build.props','global.json','.gitignore','.dockerignore','.gitattributes','.github/workflows/verify.yml','tools/release-artifacts.cjs','tools/quality-schema-contract.cjs','tools/release-preflight.cjs','tools/verify-project-artifacts.py','tools/check-dependency-audit.py','tools/New-InstitutionStaffTestInstance.ps1','tools/Bind-StaffDisposableFixtureRetention.ps1','tools/Invoke-QualityIntegration.ps1','tools/local_test_db.py',...scan('migrations').filter(f=>f.endsWith('/002_validate.sql')||f.includes('candidate-r3/')&&f.endsWith('.sql'))];
function scan(dir){return fs.readdirSync(path.join(root,dir),{withFileTypes:true}).flatMap(e=>e.isDirectory()?(['bin','obj','private'].includes(e.name)?[]:scan(dir+'/'+e.name)):[dir+'/'+e.name]);}
const files=[...new Set([...roots.flatMap(scan).filter(f=>(/\.(cs|csproj|sql|json|config)$/.test(f)||f.startsWith('infra/')&&/\.(yaml|yml|py|sh|conf)$/.test(f))&&(!f.includes('appsettings')||f.endsWith('appsettings.example.json'))),...explicit])].sort();
function canonical(bytes){return Buffer.from(bytes.toString('utf8').replaceAll('\r\n','\n'),'utf8');}
function hash(bytes){return crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase();}
const records=Object.fromEntries(files.map(f=>[f,hash(canonical(fs.readFileSync(path.join(root,f))))]));
const bootstrap=JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json'),'utf8'));
const inventory={tables:Object.keys(bootstrap.tables).length,triggers:Object.keys(bootstrap.triggers).length,fks:Object.values(bootstrap.tables).reduce((n,ddl)=>n+[...ddl.matchAll(/^\s+CONSTRAINT .*FOREIGN KEY/gm)].length,0),indexes:Object.values(bootstrap.tables).reduce((n,ddl)=>n+[...ddl.matchAll(/^\s+(?:PRIMARY KEY|UNIQUE KEY|KEY) /gm)].length,0)};
if(JSON.stringify(bootstrap.inventory)!==JSON.stringify(inventory))throw Error('Bootstrap inventory contradicts its definitions');
const migrations=['001_identity_account.sql','002_profile_acquisition.sql','003_password_attempt.sql','004_release_schema.sql','005_release_gate.sql'].map(f=>'migrations/identity-account/'+f);
if(JSON.stringify(files.filter(f=>f.startsWith('migrations/identity-account/')&&f.endsWith('.sql')))!==JSON.stringify(migrations))throw Error('Unexpected or missing canonical migration');
const metadata={format:2,release:'er-quality-1',hashEncoding:'UTF8 CRLF normalized to LF; case-sensitive POSIX relative paths',historicalEvidence:'migrations/bootstrap/manifest.json',bootstrapInventory:JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json'),'utf8')).inventory,requiredMigrations:['migrations/bootstrap/001_schema.sql',...migrations]};
if(process.argv.includes('--write')){
 fs.mkdirSync(path.dirname(manifestPath),{recursive:true});
 fs.writeFileSync(manifestPath,JSON.stringify({...metadata,files:records},null,2)+'\n');
}else{
 const manifest=JSON.parse(fs.readFileSync(manifestPath,'utf8'));
 if(JSON.stringify(Object.keys(manifest).sort())!==JSON.stringify([...Object.keys(metadata),'files'].sort()))throw Error('Invalid release manifest schema');
 for(const [key,expected] of Object.entries(metadata))
  if(JSON.stringify(manifest[key])!==JSON.stringify(expected))throw Error('Invalid release manifest metadata: '+key);
 if(JSON.stringify(manifest.files)!==JSON.stringify(records))throw Error('Release closure/hash mismatch; review differences before --write');
 const ignored=cp.spawnSync('git',['check-ignore','--no-index','--stdin','-z'],{cwd:root,input:files.join('\0')+'\0',encoding:'utf8'});
 if(ignored.status!==1)throw Error('Required file ignored or Git check failed: '+ignored.stdout.replaceAll('\0','\n'));
 for(const file of files){
  const target=path.resolve(root,file);if(!target.startsWith(root+path.sep)||file.includes('\\'))throw Error('Unsafe manifest path');
  const bytes=fs.readFileSync(target),lf=canonical(bytes),crlf=Buffer.from(lf.toString('utf8').replaceAll('\n','\r\n'));
  if(hash(canonical(lf))!==hash(canonical(crlf)))throw Error('Cross-platform hash drift: '+file);
 }
 // Packaging must carry all EmbeddedResource globs plus the release SQL artifact.
 const docker=fs.readFileSync(path.join(root,'infra/Dockerfile'),'utf8');
 for(const dir of ['migrations/mt1/','migrations/mt3/','migrations/bootstrap/','migrations/identity-account/'])
  if(!docker.includes('COPY '+dir))throw Error('Docker input closure missing '+dir);
 if(!docker.includes('COPY migrations/release/additive-contract.json'))throw Error('Docker additive contract missing');
 cp.execFileSync(process.execPath,[path.join(root,'tools/quality-schema-contract.cjs')],{stdio:'inherit'});
}
console.log(`${files.length} canonical release artifacts verified; LF/CRLF invariant; no SQL executed`);
