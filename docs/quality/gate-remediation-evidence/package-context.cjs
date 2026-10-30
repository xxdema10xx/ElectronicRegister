const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../../..'),i=JSON.parse(fs.readFileSync(__dirname+'/instance.json','utf8').replace(/^\uFEFF/,''));
const target=path.join(i.Run,'docker-context-final');
if(fs.existsSync(target))throw Error('Packaging evidence cannot be overwritten');
fs.mkdirSync(target);
for(const name of ['Directory.Build.props','global.json'])fs.copyFileSync(path.join(root,name),path.join(target,name));
fs.cpSync(path.join(root,'Backend/ElectronicRegisterAPI/src'),path.join(target,'Backend/ElectronicRegisterAPI/src'),{recursive:true,filter:p=>!p.split(path.sep).some(v=>['bin','obj'].includes(v))&&!/appsettings.*\.json$/.test(p)&&!p.endsWith('.local.json')});
for(const dir of ['mt1','mt3','identity-account']){
 const destination=path.join(target,'migrations',dir);fs.mkdirSync(destination,{recursive:true});
 for(const file of fs.readdirSync(path.join(root,'migrations',dir)).filter(f=>f.endsWith('.sql')))fs.copyFileSync(path.join(root,'migrations',dir,file),path.join(destination,file));
}
fs.mkdirSync(path.join(target,'migrations/bootstrap'),{recursive:true});
for(const file of ['001_schema.sql','schema-contract.json'])fs.copyFileSync(path.join(root,'migrations/bootstrap',file),path.join(target,'migrations/bootstrap',file));
fs.mkdirSync(path.join(target,'migrations/release'),{recursive:true});
fs.copyFileSync(path.join(root,'migrations/release/manifest.json'),path.join(target,'migrations/release/manifest.json'));
console.log('Clean Docker-equivalent source context prepared; no obj/bin/runtime secrets copied.');
