// Generate a reviewable, environment-bound SQL bundle. Never connects or executes SQL.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),config=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,'')),out=path.resolve(process.argv[3]||'');
for(const key of ['database','definerUser','definerHost','apiUser','apiHost','reviewUser','reviewHost'])if(!/^[A-Za-z0-9_.%-]+$/.test(config[key]||''))throw Error('Explicit safe principal/database required: '+key);
if([config.apiUser,config.reviewUser,config.definerUser].includes('root')||new Set([config.apiUser,config.reviewUser,config.definerUser]).size!==3)throw Error('Separate non-root principals required');
if(!process.argv[3]||fs.existsSync(out))throw Error('A NEW output directory is required');
const sourceRoot=path.resolve(config.artifactRoot||root),manifest=JSON.parse(fs.readFileSync(path.join(sourceRoot,'migrations/release/manifest.json'),'utf8'));
const hash=s=>crypto.createHash('sha256').update(s.replaceAll('\r\n','\n')).digest('hex').toUpperCase();
const canonical=['migrations/bootstrap/001_schema.sql','001_identity_account.sql','002_profile_acquisition.sql','003_password_attempt.sql','004_release_schema.sql','005_release_gate.sql'].map((f,n)=>n?'migrations/identity-account/'+f:f);
if(manifest.format!==2||manifest.release!=='er-quality-1'||JSON.stringify(manifest.requiredMigrations)!==JSON.stringify(canonical))throw Error('Invalid input release closure');
const account=prefix=>"'"+config[prefix+'User']+"'@'"+config[prefix+'Host']+"'",db='`'+config.database+'`',owner=account('definer');
const records=[];fs.mkdirSync(out,{recursive:true});
const additive=JSON.parse(fs.readFileSync(path.join(sourceRoot,'migrations/release/additive-contract.json'),'utf8'));
if(hash(fs.readFileSync(path.join(sourceRoot,'migrations/release/additive-contract.json'),'utf8'))!==manifest.files['migrations/release/additive-contract.json'])throw Error('Additive contract hash mismatch');
let index=0;
for(const file of canonical){let source=fs.readFileSync(path.join(sourceRoot,file),'utf8');if(hash(source)!==manifest.files[file])throw Error('Input SQL hash mismatch: '+file);
 const settings=file.includes('/identity-account/')?`SET SESSION sql_mode='${additive.triggers[0].SQL_MODE}';\nSET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;\n`:'';
 const sql='USE '+db+';\n'+settings+source.replace(/CREATE DEFINER=`[^`]+`@`[^`]+` TRIGGER /g,'CREATE DEFINER='+owner+' TRIGGER ').replace(/CREATE TRIGGER /g,'CREATE DEFINER='+owner+' TRIGGER ').replace(/CREATE PROCEDURE /g,'CREATE DEFINER='+owner+' PROCEDURE ');
 const name=String(++index).padStart(2,'0')+'-'+path.basename(file);fs.writeFileSync(path.join(out,name),sql);records.push({source:file,sourceHash:hash(source),artifact:name,artifactHash:hash(sql)});
}
// API/reviewer login accounts must be pre-created with secrets by the operator.
// The SQL definer cannot log in. Migration identity alone executes these files.
fs.writeFileSync(path.join(out,'00-principals.sql'),`CREATE USER ${owner} ACCOUNT LOCK;
GRANT SELECT,INSERT,UPDATE,DELETE,TRIGGER ON ${db}.* TO ${owner};
GRANT SELECT,INSERT,UPDATE,DELETE ON ${db}.* TO ${account('api')};
GRANT SELECT,TRIGGER ON ${db}.* TO ${account('review')};
GRANT SELECT(User,Host) ON mysql.user TO ${account('review')};
GRANT SELECT ON mysql.proc TO ${account('review')};
`);
fs.writeFileSync(path.join(out,'99-routine-grants.sql'),`GRANT EXECUTE ON PROCEDURE ${db}.er_schema_ready TO ${owner};
GRANT EXECUTE ON PROCEDURE ${db}.er_schema_ready TO ${account('api')};
GRANT EXECUTE ON PROCEDURE ${db}.er_schema_ready TO ${account('review')};
`);
fs.writeFileSync(path.join(out,'binding.json'),JSON.stringify({format:1,release:manifest.release,database:config.database,definer:config.definerUser+'@'+config.definerHost,api:config.apiUser+'@'+config.apiHost,review:config.reviewUser+'@'+config.reviewHost,records},null,2)+'\n');
console.log('Bound SQL bundle generated for review. No SQL executed; historical source unchanged.');
