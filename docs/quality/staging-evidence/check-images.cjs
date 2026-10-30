// Public registry metadata only. Does not pull layers, start containers or prove TLS support.
const fs=require('fs');
(async()=>{
 const auth=await fetch('https://auth.docker.io/token?service=registry.docker.io&scope=repository:library/mysql:pull');if(!auth.ok)throw Error('Registry authentication metadata unavailable');const {token}=await auth.json();
 const headers={Authorization:'Bearer '+token,Accept:'application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.docker.distribution.manifest.v2+json, application/vnd.oci.image.index.v1+json, application/vnd.oci.image.manifest.v1+json'};
 async function manifest(ref){const r=await fetch('https://registry-1.docker.io/v2/library/mysql/manifests/'+ref,{headers});if(!r.ok)throw Error('MySQL image manifest unavailable: '+r.status);return {body:await r.json(),digest:r.headers.get('docker-content-digest')};}
 const tag=await manifest('5.7.24');let selected=tag;let platforms=[];
 if(tag.body.manifests){platforms=tag.body.manifests.map(m=>m.platform);const amd=tag.body.manifests.find(m=>m.platform.os==='linux'&&m.platform.architecture==='amd64');if(!amd)throw Error('No linux/amd64 image');selected=await manifest(amd.digest);}
 const r=await fetch('https://registry-1.docker.io/v2/library/mysql/blobs/'+selected.body.config.digest,{headers});if(!r.ok)throw Error('Image configuration unavailable');const config=await r.json();
 const sdk=await fetch('https://mcr.microsoft.com/v2/dotnet/sdk/manifests/10.0-noble',{headers:{Accept:headers.Accept}});
 const output={checkedAt:new Date().toISOString(),mysqlTag:'mysql:5.7.24',tagDigest:tag.digest,amd64Digest:selected.digest,platforms,architecture:config.architecture,versionEnv:config.config.Env.filter(e=>e.startsWith('MYSQL_VERSION=')),sdkNobleManifestStatus:sdk.status,containersExecuted:false,mysqlTlsVerified:false};
 fs.writeFileSync(__dirname+'/registry-metadata.json',JSON.stringify(output,null,2));console.log(JSON.stringify(output));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
