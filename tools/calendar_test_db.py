"""Calendar migration verifier for freshly created workspace MySQL 5.7 instances only."""
import argparse
import json
import os
from pathlib import Path
import subprocess
from formative_test_db import Instance, ROOT, BIN, DB, HIDDEN, require, sha

def apply(run):
    instance=Instance(run)
    identity=instance.attest()
    before=instance.contract()
    expected=json.loads((ROOT/'migrations/institution-calendar/predecessor-contract.json').read_text())
    require(before['inventory']==expected['inventory'],'Wrong calendar predecessor inventory')
    require(before['tables']==expected['tables'],'Wrong calendar predecessor table definitions')
    require(set(before['trigger_metadata'])==set(expected['trigger_metadata']),'Wrong predecessor triggers')
    for name,row in before['trigger_metadata'].items():
        for key,value in row.items():
            wanted=expected['trigger_metadata'][name][key]
            if key=='ACTION_STATEMENT':
                wanted=wanted.replace('@@port=13306',f'@@port={instance.port}').replace("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'",f"LOWER(REPLACE(@@datadir,CHAR(92),'/'))='{Path(instance.identity['datadir']).as_posix().lower()}/'")
            require(str(value or '')==str(wanted or ''),'Predecessor trigger mismatch '+name+'/'+key)
    require(instance.rows('SELECT COUNT(*) AS n FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();')[0]['n']=='0','Other clients connected')
    backup=instance.run/'calendar-predecessor-backup.sql'
    require(not backup.exists(),'Backup exists: do not replay DDL')
    env=dict(os.environ,MYSQL_PWD=(instance.run/'password.txt').read_text())
    with backup.open('xb') as output:
        result=subprocess.run([str(BIN/'mysqldump.exe'),'--no-defaults','--protocol=TCP','--host=127.0.0.1',f'--port={instance.port}','--user=root','--single-transaction','--routines','--triggers','--hex-blob',DB],stdout=output,stderr=subprocess.PIPE,env=env,creationflags=HIDDEN)
    require(result.returncode==0 and backup.stat().st_size>0,'Backup failed')
    (instance.run/'calendar-backup-sha256.txt').write_text(sha(backup))
    instance.attest()
    instance.client((ROOT/'migrations/institution-calendar/001_expand.sql').read_text())
    after=instance.contract()
    require(all(after['tables'][k]==v for k,v in before['tables'].items()),'Previous table changed')
    require(all(after['trigger_metadata'][k]==v for k,v in before['trigger_metadata'].items()),'Previous trigger changed')
    post=instance.client((ROOT/'migrations/institution-calendar/002_validate.sql').read_text())
    require(all(line.split('\t')[-1]=='0' for line in post.strip().splitlines()),'Calendar postflight failed')
    (instance.run/'calendar-postflight.tsv').write_text(post)
    (instance.run/'calendar-contract.json').write_text(json.dumps(after,indent=2))
    (instance.run/'calendar-migration-evidence.json').write_text(json.dumps(dict(identity=identity,pid=instance.identity['pid'],backupSha256=sha(backup),before=before['inventory'],after=after['inventory'],previousDefinitionsPreserved=True),indent=2))
    print('Calendar migration verified on isolated instance:',instance.port,after['inventory'])

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run',required=True)
    args=parser.parse_args()
    apply(args.run)
