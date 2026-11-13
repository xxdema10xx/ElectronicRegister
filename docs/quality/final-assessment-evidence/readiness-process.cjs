const fs=require('fs'),path=require('path'),cp=require('child_process'),net=require('net');
(async()=>{
 const i=JSON.parse(fs.readFileSync(__dirname+'/instance.json','utf8').replace(/^\uFEFF/,'')),run=i.Run;
 const expected=+process.argv[2],name=process.argv[3];if(![200,503].includes(expected)||!/^[a-z0-9-]+$/.test(name))throw Error('Expected status and run name required');
 const listener=net.createServer();await new Promise(resolve=>listener.listen(0,'127.0.0.1',resolve));const port=listener.address().port;await new Promise(resolve=>listener.close(resolve));
 const config={urls:`http://127.0.0.1:${port}`,Deployment:{Mode:'LocalTest',LocalTestDatabasePort:i.Port},ConnectionStrings:{DefaultConnection:`Server=127.0.0.1;Port=${i.Port};Database=electronic_register_test;User ID=root;Password=${fs.readFileSync(path.join(run,'password.txt'),'utf8')};SslMode=None`},Jwt:{Key:'disposable-readiness-verification-key-0123456789',Issuer:'verification',Audience:'verification'},Readiness:{ExpectedDataDir:path.join(run,'data')},Logging:{LogLevel:{Default:'Critical'},EventLog:{LogLevel:{Default:'None'}}}};
 const file=path.join(run,name+'-runtime.json');fs.writeFileSync(file,JSON.stringify(config),{flag:'wx'});
 const log=fs.openSync(path.join(run,name+'-api.log'),'wx');
 const api=cp.spawn('dotnet',['Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/bin/Release/net10.0/ElectronicRegisterAPI.dll','--environment=Production'],{env:{...process.env,ER_RUNTIME_CONFIG:file},stdio:['ignore',log,log],windowsHide:true});
 try{
  let live;const deadline=Date.now()+20000;while(Date.now()<deadline){try{live=await fetch(`http://127.0.0.1:${port}/health/live`);break}catch{await new Promise(r=>setTimeout(r,100));}}
  if(live?.status!==200)throw Error('API startup failed; inspect private log');
  const ready=await fetch(`http://127.0.0.1:${port}/health/ready`);
  fs.writeFileSync(__dirname+'/'+name+'.json',JSON.stringify({Liveness:live.status,Readiness:ready.status,Expected:expected,Port:i.Port,DDLExecutedByProbe:false},null,2));
  if(ready.status!==expected)throw Error('Unexpected readiness status '+ready.status);
  console.log(`Real Program: live=200, readiness=${ready.status}; expected ${expected}`);
 }finally{api.kill();await new Promise(resolve=>api.once('exit',resolve));fs.closeSync(log);}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
