// In-memory mutation only: no manifest, Git index or application source is modified.
const fs=require('fs'),path=require('path'),Module=require('module');
const root=path.resolve(__dirname,'../../..'),file=path.join(root,'tools/release-artifacts.cjs'),target=path.join(root,'migrations/release/manifest.json');
const original=JSON.parse(fs.readFileSync(target,'utf8'));
const invalid={...original,format:999,release:'wrong-release',requiredMigrations:[]};
const mod=new Module(file,module);mod.filename=file;mod.paths=Module._nodeModulePaths(path.dirname(file));
mod.require=id=>id==='fs'?{...fs,readFileSync:(f,...args)=>path.resolve(f)===target?JSON.stringify(invalid):fs.readFileSync(f,...args)}:require(id);
mod._compile(fs.readFileSync(file,'utf8'),file);
fs.writeFileSync(__dirname+'/manifest-negative-control.json',JSON.stringify({DiskManifestUnchanged:true,InvalidFormat:999,InvalidRelease:'wrong-release',RequiredMigrations:[],VerifierAccepted:true},null,2));
