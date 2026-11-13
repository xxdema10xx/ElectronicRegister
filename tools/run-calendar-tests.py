"""Run real tests only against an attested fresh workspace instance; preserve historical generated artifacts."""
import argparse,json,os,re,subprocess,sys,uuid
from pathlib import Path
from formative_test_db import Instance,ROOT,BIN,require

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--run',required=True);p.add_argument('--name',required=True);p.add_argument('--filter',default='FullyQualifiedName~Calendar_');p.add_argument('--full',action='store_true');a=p.parse_args()
    require(re.fullmatch('[a-zA-Z0-9_-]+',a.name),'Safe result name required')
    i=Instance(a.run);i.attest();run=i.run
    files=subprocess.check_output(['git','ls-files','tests/Integration/TestResults'],cwd=ROOT).decode().splitlines()
    snapshot=run/('snapshot-'+uuid.uuid4().hex);snapshot.mkdir()
    for index,name in enumerate(files):(snapshot/str(index)).write_bytes((ROOT/name).read_bytes())
    (snapshot/'index.json').write_text(json.dumps(files))
    env=dict(os.environ,TEST_MYSQL_CONNECTION=f'Server=127.0.0.1;Port={i.port};Database=electronic_register_test;User ID=root;Password={(run/"password.txt").read_text()};Allow User Variables=true;SslMode=None',ER_TEST_ISOLATED_PORT=str(i.port),ER_TEST_ISOLATED_DATADIR=i.identity['datadir'],ER_TEST_STRICT_SESSION='1',MYSQLDUMP_EXE=str(BIN/'mysqldump.exe'),MYSQLDUMP_FLAVOR='MySQL57')
    env.pop('ER_MT5B_SQL_PROOF',None)
    args=['dotnet','test','tests/Integration/Integration.Tests.csproj','--no-restore','--blame-hang-timeout','120s','--blame-hang-dump-type','none','--logger','trx;LogFileName='+a.name+'.trx','--logger','console;verbosity=normal','--results-directory',str(run/'results')]
    if not a.full:args+=['--filter',a.filter]
    log=run/(a.name+'.log');require(not log.exists(),'Result name already used')
    (run/(a.name+'-source-manifest.json')).write_bytes((ROOT/'migrations/bootstrap/manifest.json').read_bytes())
    try:
        with log.open('wb') as output:result=subprocess.run(args,cwd=ROOT,env=env,stdout=output,stderr=subprocess.STDOUT)
    finally:
        changed=[]
        for index,name in enumerate(files):
            before=(snapshot/str(index)).read_bytes();path=ROOT/name
            if path.read_bytes()!=before:
                changed.append(name);(snapshot/(str(index)+'.generated')).write_bytes(path.read_bytes());path.write_bytes(before)
        (snapshot/'preservation.json').write_text(json.dumps(dict(regenerated=changed,initialBytesRestored=True),indent=2))
    print(log.read_text(encoding='utf-8',errors='replace')[-6500:]);sys.exit(result.returncode)
if __name__=='__main__':main()
