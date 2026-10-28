"""Attest and compare two disposable native schemas after tests; optionally stop their exact processes."""
import argparse,json
from pathlib import Path
from formative_test_db import Instance,ROOT,require,sha
from calendar_schema_contract import phase4_contract


def capture(instance):
    live=instance.attest()
    require(instance.rows('SELECT COUNT(*) AS n FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();')[0]['n']=='0','Tests or other clients still connected')
    actual=instance.contract()
    previous=json.loads((ROOT/'migrations/institution-calendar/predecessor-contract.json').read_text())
    def bind(value):
        if isinstance(value,str):
            return value.replace('@@port=13306',f'@@port={instance.port}').replace("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'",f"LOWER(REPLACE(@@datadir,CHAR(92),'/'))='{Path(instance.identity['datadir']).as_posix().lower()}/'")
        if isinstance(value,dict):return {k:bind(v) for k,v in value.items()}
        return value
    for category in ('tables','triggers','trigger_metadata'):
        for name,value in previous[category].items():
            # mysql --xml exposes numeric metadata as text; the frozen JSON
            # retains ACTION_ORDER as an integer. Compare its exact value.
            if category=='trigger_metadata' and isinstance(value.get('ACTION_ORDER'),int):
                actual[category][name]['ACTION_ORDER']=int(actual[category][name]['ACTION_ORDER'])
            require(actual[category][name]==bind(value),'Predecessor changed: '+category+'/'+name)
            actual[category][name]=value
    phase4_contract(ROOT,actual)
    for source,target,count in [('institution-calendar','calendar-postflight-final.tsv',5),('institution-formative-plan','formative-postflight-final.tsv',10)]:
        output=instance.client((ROOT/'migrations'/source/'002_validate.sql').read_text())
        rows=output.strip().splitlines()
        require(len(rows)==count and all(row.split('\t')[-1]=='0' for row in rows),'Postflight violations: '+source)
        (instance.run/target).write_text(output)
    (instance.run/'calendar-normalized-contract.json').write_text(json.dumps(actual,indent=2)+'\n')
    return actual,dict(identity=live,pid=instance.identity['pid'],binaryPath=r'C:\MAMP\bin\mysql\bin\mysqld.exe',predecessorDefinitionsPreserved=True,calendarPostflightViolations=0,formativePostflightViolations=0)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--migration-run',required=True);p.add_argument('--bootstrap-run',required=True);p.add_argument('--stop',action='store_true');a=p.parse_args()
    m=Instance(a.migration_run);b=Instance(a.bootstrap_run)
    require(m.port!=b.port and m.identity['uuid']!=b.identity['uuid'],'Independent instances required')
    mc,me=capture(m);bc,be=capture(b)
    require(mc==bc,'Migration and independent bootstrap differ')
    require(mc==json.loads((ROOT/'migrations/bootstrap/schema-contract.json').read_text()),'Live schema differs from canonical contract')
    backup=m.run/'calendar-predecessor-backup.sql'
    me['backupSha256']=sha(backup)
    require(me['backupSha256']==(m.run/'calendar-backup-sha256.txt').read_text().strip(),'Backup digest changed')
    require(me['backupSha256']==json.loads((m.run/'calendar-migration-evidence.json').read_text())['backupSha256'],'Migration backup evidence mismatch')
    result=dict(independentBootstrapEqualsMigration=True,bothPostflightViolations=0,inventory=mc['inventory'],migration=me,bootstrap=be,disposableProcessesStopped=False)
    if a.stop:
        import subprocess
        for i in (m,b):
            i.attest()
            require(i.rows('SELECT COUNT(*) AS n FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();')[0]['n']=='0','Concurrent clients before shutdown')
            i.client('SHUTDOWN;',database=False)
            check=subprocess.run(['powershell','-NoProfile','-Command',f"Wait-Process -Id {int(i.identity['pid'])} -Timeout 15 -ErrorAction SilentlyContinue; if(Get-Process -Id {int(i.identity['pid'])} -ErrorAction SilentlyContinue){{exit 1}}else{{exit 0}}"],capture_output=True)
            require(check.returncode==0,'Native process still running')
        result['disposableProcessesStopped']=True
    (b.run/'calendar-final-database-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))


if __name__=='__main__':main()
