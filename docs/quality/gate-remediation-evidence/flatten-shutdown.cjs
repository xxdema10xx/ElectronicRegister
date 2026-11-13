// Correct PowerShell 5.1's serialized array wrapper; preserve the original records.
const fs=require('fs'),p=__dirname+'/shutdown-final.json',raw=fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'');
fs.writeFileSync(__dirname+'/shutdown-serialization-original.json',raw,{flag:'wx'});
const rows=JSON.parse(raw).flatMap(row=>Array.isArray(row.value)?row.value:row);
if(rows.length!==5||new Set(rows.map(r=>r.Port)).size!==5||rows.some(r=>!r.Stopped||!r.IdentityVerified))throw Error('Incomplete underlying shutdown records');
fs.writeFileSync(p,JSON.stringify(rows,null,2)+'\n');
