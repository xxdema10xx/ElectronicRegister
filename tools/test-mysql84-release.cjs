'use strict';
const fs=require('fs'),path=require('path'),Module=require('module'),assert=require('assert');
const root=path.resolve(__dirname,'..'),file=path.join(__dirname,'mysql84-release.cjs');
const target=path.join(root,'migrations/mysql84-remediation/manifest.json');
const original=JSON.parse(fs.readFileSync(target));
const cp=require('child_process'),release=require('./mysql84-release.cjs');
const tracked=new Set(cp.execFileSync('git',['ls-files','--cached','--others','--exclude-standard','-z'],{cwd:root,encoding:'utf8'}).split('\0'));
const names=Object.keys(original.files);
const ignored=cp.spawnSync('git',['check-ignore','--no-index','-z','--stdin'],
 {cwd:root,input:names.join('\0')+'\0',encoding:'utf8'});
assert.equal(ignored.status,1,'Manifest contains ignored files: '+ignored.stdout+ignored.stderr);
for(const [name,expected] of Object.entries(original.files)){
 assert(!/(^|\/)(\.staff-private|\.formative-private)(\/|$)/.test(name),name);
 assert(tracked.has(name),'Unavailable in a clean checkout (not tracked or addable): '+name);
 assert(fs.statSync(path.join(root,name)).isFile(),name);
 assert.equal(release.sha(fs.readFileSync(path.join(root,name))),expected,name);
}
release.verify();
console.log(names.length+' publishable closure files and checksums verified');
const mutations={release:m=>m.release='er-quality-1',predecessor:m=>m.predecessor='unknown',
 revision:m=>m.predecessorRevision='0'.repeat(40),engine:m=>m.mysql.version='8.4.3',
 digest:m=>m.mysql.image='mysql:latest',omission:m=>m.changedTriggers.pop(),order:m=>m.changedTriggers.reverse(),
 file:m=>delete m.files['tools/mysql84_release.py'],hash:m=>m.files['tools/mysql84_release.py']='0'.repeat(64),
 privateStaff:m=>m.files['.staff-private/injected.json']='0'.repeat(64),
 privateFormative:m=>m.files['.formative-private/injected.json']='0'.repeat(64),
 extra:m=>m.unreviewed=true};
for(const [name,mutate] of Object.entries(mutations)){
 const value=structuredClone(original);mutate(value);
 const mod=new Module(file,module);mod.filename=file;mod.paths=Module._nodeModulePaths(__dirname);
 mod.require=id=>id==='fs'?{...fs,readFileSync:(f,...args)=>path.resolve(f)===target?JSON.stringify(value):fs.readFileSync(f,...args)}:require(id);
 mod._compile(fs.readFileSync(file,'utf8'),file);
 assert.throws(()=>mod.exports.verify(),undefined,name+' accepted');
}
// Simulate a tracked source edit through the filesystem seam without touching disk.
{
 const source=path.join(root,'tools/mysql84_release.py');
 const mod=new Module(file,module);mod.filename=file;mod.paths=Module._nodeModulePaths(__dirname);
 mod.require=id=>id==='fs'?{...fs,readFileSync:(f,...args)=>path.resolve(f)===source
  ?Buffer.concat([fs.readFileSync(f),Buffer.from('\n# unrecorded tracked edit\n')]):fs.readFileSync(f,...args)}:require(id);
 mod._compile(fs.readFileSync(file,'utf8'),file);
 assert.throws(()=>mod.exports.verify(),/New release closure drift/,'Tracked edit accepted');
}
console.log(Object.keys(mutations).length+' manifest negative controls and tracked-source drift passed; no disk mutation');
// Build the actual environment-bound fresh and predecessor artifacts locally.
// No MySQL client or network is involved in these generation tests.
const os=require('os');
const temporary=fs.mkdtempSync(path.join(os.tmpdir(),'er84-release-bundle-'));
try{
 const config={database:'er84_synthetic',definerUser:'synthetic_owner',definerHost:'%',apiUser:'synthetic_api',apiHost:'%',reviewUser:'synthetic_review',reviewHost:'%'};
 const release=require('./mysql84-release.cjs'),modern=path.join(temporary,'new'),legacy=path.join(temporary,'old');
 release.bundle(config,modern);release.bundle(config,legacy,false);
 const binding=JSON.parse(fs.readFileSync(path.join(modern,'binding.json')));
 assert.equal(binding.release,'er-quality-2-mysql84');
 assert.equal(binding.database,config.database);
 assert(binding.files['98-new-release.sql']);
 for(const [name,expected] of Object.entries(binding.files)){
  const sql=fs.readFileSync(path.join(modern,name),'utf8');
  assert.equal(release.sha(sql),expected);
  assert(!require('./mysql84-remediation.cjs').forbidden.test(sql));
 }
 const oldBinding=JSON.parse(fs.readFileSync(path.join(legacy,'binding.json')));
 assert.equal(oldBinding.release,'er-quality-1');
 assert(!oldBinding.files['98-new-release.sql']);
 const oldBootstrap=fs.readFileSync(path.join(legacy,oldBinding.records[0].artifact),'utf8');
 const newBootstrap=fs.readFileSync(path.join(modern,binding.records[0].artifact),'utf8');
 assert(require('./mysql84-remediation.cjs').forbidden.test(oldBootstrap));
 assert.equal((newBootstrap.match(/REGEXP_LIKE\(/g)||[]).length,22);
 assert(newBootstrap.includes("'^[0-9A-F]{64}\\\\z'"));
 assert(!newBootstrap.includes('NO_AUTO_CREATE_USER'));
 assert(newBootstrap.includes("BINARY NEW.status"));
 assert(fs.readFileSync(path.join(modern,'00-principals.sql'),'utf8').includes('GRANT SELECT,INSERT,UPDATE,DELETE ON `er84_synthetic`.*'));
 console.log('Fresh and predecessor SQL bundles generated and verified offline; runtime NOT tested');
}finally{
 // Only the fresh directory created above can be removed; never a user path.
 const actual=fs.realpathSync(temporary),parent=fs.realpathSync(os.tmpdir());
 assert.equal(path.dirname(actual),parent);
 assert(path.basename(actual).startsWith('er84-release-bundle-'));
 fs.rmSync(actual,{recursive:true});
}
// er-platform-identity-1 successor: DEFINER binding, unbound fixture predicate in
// ordinary bundles, fixture-only binding and rejection of unsafe configurations.
{
 const os=require('os');
 const base={database:'electronic_register_test',definerUser:'er_definer',definerHost:'localhost'};
 const plain=release.successorSql(base);
 assert(plain.startsWith('USE `electronic_register_test`;\n'));
 assert.equal((plain.match(/CREATE DEFINER='er_definer'@'localhost' TRIGGER /g)||[]).length,6);
 assert(!/CREATE TRIGGER /.test(plain));
 assert.equal((plain.match(/VERSION\(\)='5\.7\.24' AND @@port=13306/g)||[]).length,2,'ordinary successor keeps the unsatisfiable 8.4 predicate');
 assert(plain.trimEnd().endsWith("INSERT INTO er_schema_release(version) VALUES('er-platform-identity-1');"),'marker written last');
 const uuid='11111111-2222-3333-4444-555555555555',host='a1b2c3d4e5f6',capability='A'.repeat(64);
 const bound=release.successorSql({...base,host:'127.0.0.1',port:23456,ownedFixture:{uuid,host,capability}});
 assert(!bound.includes("VERSION()='5.7.24'"));
 assert.equal((bound.match(new RegExp(`BINARY @er_mt5b_fixture_capability='${capability}'`,'g'))||[]).length,2);
 for(const bad of [{...base,definerUser:'root'},{...base,definerHost:"x'y"},{...base,database:''}])
  assert.throws(()=>release.successorSql(bad),undefined,JSON.stringify(bad));
 assert.throws(()=>release.successorSql({...base,host:'127.0.0.1',port:3307,ownedFixture:{uuid,host,capability}}),/Owned fixture binding rejected/);
 const out=path.join(fs.mkdtempSync(path.join(os.tmpdir(),'er84-successor-')),'bundle');
 try{
  release.successor(base,out);
  const binding=JSON.parse(fs.readFileSync(path.join(out,'binding.json')));
  assert.equal(binding.release,'er-platform-identity-1');assert.equal(binding.predecessor,'er-quality-2-mysql84');
  assert.equal(binding.files['01-platform-identity.sql'],release.sha(fs.readFileSync(path.join(out,'01-platform-identity.sql'))));
  assert.throws(()=>release.successor(base,out),/NEW output directory required/);
 }finally{fs.rmSync(path.dirname(out),{recursive:true});}
 console.log('platform identity successor binding verified');
}
// er-grade-scale-1 successor: independent of platform identity, applied after it; no triggers, conversion and
// marker in one transaction, every UPDATE guarded by the marker, bundled after platform identity.
{
 const os=require('os');
 const base={database:'electronic_register_test',definerUser:'er_definer',definerHost:'localhost'};
 assert.deepEqual(release.successors.map(s=>s.release),['er-platform-identity-1','er-grade-scale-1']);
 assert.deepEqual(release.successors.map(s=>s.bundleFile),[...release.successors.map(s=>s.bundleFile)].sort(),'bundle files sort in application order');
 assert(release.successors.every(s=>s.bundleFile<'98-new-release.sql'));
 const sql=release.successorSql(base,'er-grade-scale-1');
 assert(sql.startsWith('USE `electronic_register_test`;\n'));
 assert(!/TRIGGER /.test(sql));
 const block=sql.slice(sql.indexOf('START TRANSACTION;'));
 assert(block.trimEnd().endsWith("INSERT INTO er_schema_release(version) VALUES('er-grade-scale-1');\nCOMMIT;"),'marker committed with the conversion');
 const code=block.split('\n').filter(l=>!l.startsWith('--')).join('\n');
 const updates=code.split(';').filter(s=>/^\s*UPDATE /.test(s));
 assert.equal(updates.length,3);
 for(const u of updates)assert(u.includes("NOT EXISTS(SELECT 1 FROM er_schema_release WHERE version='er-grade-scale-1')"),'conversion guarded by the marker');
 assert.throws(()=>release.successorSql(base,'er-unknown-1'),/Unknown successor release/);
 const out=path.join(fs.mkdtempSync(path.join(os.tmpdir(),'er84-grade-scale-')),'bundle');
 try{
  release.successor(base,out,'er-grade-scale-1');
  const binding=JSON.parse(fs.readFileSync(path.join(out,'binding.json')));
  assert.equal(binding.release,'er-grade-scale-1');assert.equal(binding.predecessor,'er-quality-2-mysql84');
  assert.equal(binding.files['01-grade-scale.sql'],release.sha(fs.readFileSync(path.join(out,'01-grade-scale.sql'))));
 }finally{fs.rmSync(path.dirname(out),{recursive:true});}
 console.log('grade scale successor binding verified');
}
