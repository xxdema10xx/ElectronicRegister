// In-memory negative controls invoke the real verifier, never rewrite a manifest.
const fs=require('fs'),path=require('path'),Module=require('module'),assert=require('assert');
const root=path.resolve(__dirname,'..'),file=path.join(root,'tools/release-artifacts.cjs'),target=path.join(root,'migrations/release/manifest.json'),source=fs.readFileSync(file,'utf8'),original=JSON.parse(fs.readFileSync(target,'utf8'));
const mutations={format:m=>m.format=999,release:m=>m.release='wrong-release',inventory:m=>m.bootstrapInventory.tables--,historical:m=>m.historicalEvidence='wrong.json',encoding:m=>m.hashEncoding='raw',emptyMigrations:m=>m.requiredMigrations=[],duplicate:m=>m.requiredMigrations.push(m.requiredMigrations[0]),omission:m=>m.requiredMigrations.pop(),order:m=>m.requiredMigrations.reverse(),extraMigration:m=>m.requiredMigrations.push('migrations/identity-account/999_unknown.sql'),missingField:m=>delete m.release,extraField:m=>m.unreviewed=true,missingFile:m=>delete m.files[Object.keys(m.files)[0]],hash:m=>m.files[Object.keys(m.files)[0]]='0'.repeat(64)};
for(const [name,mutate]of Object.entries(mutations)){
 const candidate=structuredClone(original);mutate(candidate);
 const mod=new Module(file,module);mod.filename=file;mod.paths=Module._nodeModulePaths(path.dirname(file));
 mod.require=id=>id==='fs'?{...fs,readFileSync:(f,...args)=>path.resolve(f)===target?JSON.stringify(candidate):fs.readFileSync(f,...args)}:require(id);
 assert.throws(()=>mod._compile(source,file),undefined,'Verifier accepted '+name);
 console.log('Rejected: '+name);
}
console.log(`${Object.keys(mutations).length} real-verifier negative controls passed; no disk mutation`);
