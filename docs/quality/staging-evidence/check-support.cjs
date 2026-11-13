const fs=require('fs'),path=require('path'),http=require('http'),cp=require('child_process'),assert=require('assert'),crypto=require('crypto');
const root=path.resolve(__dirname,'../../..'),run=fs.mkdtempSync(root+'/.staff-private/staging-support-'),dll=root+'/tools/StagingSupport/bin/Release/net10.0/StagingSupport.dll';
function dotnet(mode,file){return new Promise(resolve=>{const p=cp.spawn('dotnet',[dll,mode,file],{windowsHide:true});let stdout='',stderr='';p.stdout.on('data',d=>stdout+=d);p.stderr.on('data',d=>stderr+=d);p.on('exit',exit=>resolve({exit,stdout,stderr}));});}
(async()=>{
 const cfg=run+'/runtime.json';fs.writeFileSync(cfg,JSON.stringify({AllowedHosts:'staging.consoliprojects.dev'}));let status=200,requests=0;
 const server=http.createServer((req,res)=>{requests++;assert.equal(req.headers.host,'staging.consoliprojects.dev');assert.equal(req.headers['x-forwarded-proto'],'https');res.writeHead(status,status===307?{location:'https://staging.consoliprojects.dev/health/ready'}:{});res.end();});
 await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(8080,'127.0.0.1',resolve);});
 const checks=[];try{for(const code of [200,503,307]){status=code;const result=await dotnet('health',cfg);assert.equal(result.exit,code===200?0:1);checks.push({httpStatus:code,exit:result.exit});}}finally{await new Promise(resolve=>server.close(resolve));}
 const seed=run+'/seed.json';fs.writeFileSync(seed,JSON.stringify({accounts:{synthetic:{password:crypto.randomBytes(24).toString('hex')}}}));
 const a=await dotnet('hashes',seed),b=await dotnet('hashes',seed);assert.equal(a.exit,0);assert.equal(b.exit,0);const first=JSON.parse(a.stdout).synthetic,second=JSON.parse(b.stdout).synthetic;assert.match(first,/^\$2[ab]\$11\$/);assert.notEqual(first,second);
 fs.writeFileSync(__dirname+'/support-execution.json',JSON.stringify({health:checks,loopbackMockRequests:requests,redirectNotFollowed:requests===3,bcryptCost:11,randomSaltConfirmed:true,realApiOrDockerTest:false},null,2));console.log('Health 200/503/redirect and BCrypt helper execution verified using owned loopback mock only.');
})().catch(e=>{console.error(e.message);process.exitCode=1;});
