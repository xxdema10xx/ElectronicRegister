const fs=require('fs'),path=require('path'),cp=require('child_process');
const root=path.resolve(__dirname,'../../..'),i=JSON.parse(fs.readFileSync(__dirname+'/instance.json','utf8').replace(/^\uFEFF/,'')),run=path.resolve(i.Run);
if(!i.New||!run.startsWith(path.join(root,'.staff-private','instance-'))||[3306,13306].includes(i.Port))throw Error('Fresh disposable target required');
function sql(query,overrides={}){const p=cp.spawnSync('C:/MAMP/bin/mysql/bin/mysql.exe',['--no-defaults','--protocol=TCP','--host=127.0.0.1','--port='+i.Port,'--user='+(overrides.user||'root'),'--database=electronic_register_test','--default-character-set=utf8mb4','--batch','--raw','--skip-column-names'],{input:query,env:{...process.env,MYSQL_PWD:overrides.password??fs.readFileSync(run+'/password.txt','utf8')},encoding:'utf8',maxBuffer:16*1024*1024});return {exit:p.status,stdout:p.stdout.trim(),stderr:p.stderr.trim()};}
const [v,p,d]=sql("SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/')").stdout.split('\t');if(v!=='5.7.24'||+p!==i.Port||d.replace(/\/$/,'').toLowerCase()!==path.join(run,'data').replaceAll('\\','/').toLowerCase())throw Error('SQL identity mismatch');
function execute(query){const result=sql(query);if(result.exit)throw Error(result.stderr);return result.stdout;}
module.exports={sql,execute,root,i,run};
