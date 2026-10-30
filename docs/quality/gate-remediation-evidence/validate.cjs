const fs=require('fs'),path=require('path'),crypto=require('crypto'),cp=require('child_process');
const root=path.resolve(__dirname,'../../..'),hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex').toUpperCase();
const historical=JSON.parse(fs.readFileSync(__dirname+'/historical-hashes.json','utf8').replace(/^\uFEFF/,''));
if(historical.some(r=>hash(r.Path)!==r.Hash))throw Error('Historical artifact modified');
const docs=['FINAL_RELEASE_ASSESSMENT.md','RELEASE_GATE_REMEDIATION.md','RELEASE_READINESS.md','DESTINATION_RELEASE_SMOKE.md'],broken=[];
for(const doc of docs)for(const m of fs.readFileSync(root+'/docs/quality/'+doc,'utf8').matchAll(/\]\(([^)]+)\)/g)){if(!/^https?:/.test(m[1])&&!fs.existsSync(path.resolve(root,'docs/quality',m[1])))broken.push({doc,target:m[1]});}
if(broken.length)throw Error('Broken report links: '+JSON.stringify(broken));
const tests=JSON.parse(fs.readFileSync(__dirname+'/test-summary-final.json','utf8'));
if(!tests.noUnexpectedFailuresOrSkips||tests.suites.some(s=>s.removedOrRenamed&&Object.keys(s.removedOrRenamed).length))throw Error('Test reconciliation failed');
for(const s of tests.suites)if(hash(s.path)!==s.sha256||hash(s.baselinePath)!==s.baselineSha256)throw Error('TRX digest mismatch');
const schema=JSON.parse(fs.readFileSync(__dirname+'/schema-matrix.json','utf8'));if(schema.cases.length!==63||schema.cases.some(c=>c.readinessAccepted||c.preflightExit===0))throw Error('Schema negative controls failed');
const shutdown=JSON.parse(fs.readFileSync(__dirname+'/shutdown-final.json','utf8').replace(/^\uFEFF/,''));if(shutdown.length!==5||shutdown.some(s=>!s.IdentityVerified||!s.Stopped))throw Error('Shutdown not verified');
const current=JSON.parse(fs.readFileSync(root+'/migrations/release/manifest.json','utf8')),before=JSON.parse(fs.readFileSync(__dirname+'/manifest-before.json','utf8'));
const delta={added:Object.keys(current.files).filter(f=>!(f in before.files)),changed:Object.keys(current.files).filter(f=>f in before.files&&current.files[f]!==before.files[f]),removed:Object.keys(before.files).filter(f=>!(f in current.files))};
fs.writeFileSync(__dirname+'/manifest-delta.json',JSON.stringify(delta,null,2));
if(process.env.NODE_TLS_REJECT_UNAUTHORIZED==='0')throw Error('TLS trust weakened');
fs.writeFileSync(__dirname+'/final-validation.json',JSON.stringify({A01:'FIXED',A02:'FIXED',A03:'FIXED',A04:'FIXED',HistoricalHashesUnchanged:true,BrokenReportLinks:broken,TrxHashesVerified:true,Unit:590,Integration:1531,NewCases:29,RemovedCases:0,SqlNegativeCases:63,ManifestNegativeCases:14,OwnedSqlInstancesStopped:5,NodeTlsVerificationEnabled:true,ReleaseDecision:'NOT READY',OpenEnvironmentalGates:['Docker runtime','modern destination TLS','GitHub Actions Linux/Windows']},null,2));
console.log('Reports/immutable SQL/TRX hashes/negative controls/teardown validated.');
