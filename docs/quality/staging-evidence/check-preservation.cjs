const fs=require('fs'),crypto=require('crypto'),cp=require('child_process');
const before=JSON.parse(fs.readFileSync('docs/quality/staging-evidence/manifest-before.json'));
const after=JSON.parse(fs.readFileSync('migrations/release/manifest.json'));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p,'utf8').replaceAll('\r\n','\n')).digest('hex').toUpperCase();
const preserved=Object.keys(before.files).filter(p=>p.startsWith('Backend/')||p.startsWith('migrations/'));
for(const p of preserved)if(hash(p)!==before.files[p])throw Error('Unexpected application/SQL change: '+p);
const result={checkedAt:new Date().toISOString(),preservedApplicationAndSqlFiles:preserved.length,
 added:Object.keys(after.files).filter(p=>!(p in before.files)),
 changed:Object.keys(before.files).filter(p=>after.files[p]!==before.files[p]),
 removed:Object.keys(before.files).filter(p=>!(p in after.files))};
const diff=cp.spawnSync('git',['diff','--check'],{encoding:'utf8'});
fs.writeFileSync('docs/quality/staging-evidence/diff-check.log',diff.stdout+diff.stderr);
if(diff.status!==0)throw Error('Diff whitespace failure');
result.diffCheckExit=diff.status;
fs.writeFileSync('docs/quality/staging-evidence/preservation.json',JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify(result,null,2));
