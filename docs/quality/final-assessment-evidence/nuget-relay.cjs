// Verification-only loopback transport: upstream HTTPS is validated by Node/OpenSSL.
// No credentials, SQL or external state are handled by this relay.
const http=require('http'),fs=require('fs');
const port=18763,base=`http://127.0.0.1:${port}`;
http.createServer(async(req,res)=>{try{
 const url=new URL(req.url,'https://api.nuget.org');
 if(url.hostname!=='api.nuget.org'){res.writeHead(403);return res.end();}
 const upstream=await fetch(url);
 let bytes=Buffer.from(await upstream.arrayBuffer());
 if(url.pathname.endsWith('.json'))bytes=Buffer.from(bytes.toString().replaceAll('https://api.nuget.org',base));
 res.writeHead(upstream.status,{'content-type':upstream.headers.get('content-type')||'application/octet-stream'});res.end(bytes);
 fs.appendFileSync(__dirname+'/nuget-relay-requests.log',`${new Date().toISOString()} ${upstream.status} ${url}\n`);
 }catch(e){res.writeHead(502);res.end('Upstream HTTPS unavailable');console.error(e.message);}
}).listen(port,'127.0.0.1',()=>console.log('Validated HTTPS relay listening on loopback '+port));
