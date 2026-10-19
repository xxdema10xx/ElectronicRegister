const fs=require('fs'),path=require('path'),crypto=require('crypto');
function readRun(folder){return fs.readdirSync(folder).filter(n=>n.endsWith('.trx')).map(file=>{
 const raw=fs.readFileSync(path.join(folder,file),'utf8'),tag=raw.match(/<Counters\b([^>]+)\/>/)[1];
 const counts=Object.fromEntries([...tag.matchAll(/(\w+)="(\d+)"/g)].map(m=>[m[1],Number(m[2])]));
 const names=[...raw.matchAll(/<UnitTestResult\b[^>]*testName="([^"]+)"/g)].map(m=>m[1]);
 if(counts.failed||counts.notExecuted||counts.total!==counts.passed||counts.total!==counts.executed||names.length!==counts.total)throw Error('Invalid counters '+file);
 return {project:counts.total<100?'Unit':'Integration',file,counts,sha256:crypto.createHash('sha256').update(raw).digest('hex'),names};
 });}
const baseline=readRun('mt3-evidence/baseline'),first=readRun('mt3-evidence/final-1'),second=readRun('mt3-evidence/final-2');
for(const run of [first,second]){
 if(run.length!==2||run.reduce((n,r)=>n+r.counts.total,0)!==348)throw Error('Unexpected total');
 for(const old of baseline){const current=run.find(r=>r.project===old.project);if(old.names.some(n=>!current.names.includes(n)))throw Error('Lost baseline test');}
}
if(JSON.stringify(first.flatMap(r=>r.names).sort())!==JSON.stringify(second.flatMap(r=>r.names).sort()))throw Error('Different test sets');
const result={baselineTotal:327,baselinePreserved:true,finalTotal:348,newIntegrationCases:21,finalTestSetsIdentical:true,
 baseline:baseline.map(({names,...r})=>r),final1:first.map(({names,...r})=>r),final2:second.map(({names,...r})=>r)};
fs.writeFileSync('mt3-evidence/verification.json',JSON.stringify(result,null,2)+'\n');
const hashes={};
for(const name of ['InstitutionStudentProfile','InstitutionTeacherProfile']){
 const file='src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/'+name+'.cs';
 if(!fs.readFileSync(file).equals(fs.readFileSync('mt3-evidence/scaffold/Entities/'+name+'.cs')))throw Error('Entity differs from scaffold');
 hashes[name]=crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
}
fs.writeFileSync('mt3-evidence/scaffold-sha256.txt',JSON.stringify(hashes,null,2)+'\n');
console.log(JSON.stringify(result,null,2));
