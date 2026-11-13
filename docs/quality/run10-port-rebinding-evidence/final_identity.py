import json,subprocess,sys
from pathlib import Path
sys.path.insert(0,str(Path('tools').resolve()))
from mysql84_release import Mysql,identity,admin_identity,require
from mysql84_runtime import Probes
from mysql84_recovery import socket_args,result_record
base=Path('.diagnostics/run10-local')
records=[]
attested=json.loads((base/'recovery-verified/restart-result.json').read_text())[-1]
for path in (base/'upgrade-config.json',base/'recovery-verified/fresh-config.json'):
    config=json.loads(path.read_text(encoding='utf-8-sig'))
    config['recoveryBinding']=attested['afterBinding']; config['port']=attested['attestedTcpTarget']['port']
    db=Mysql(config)
    try:
        server=identity(db,config)
        admin_identity(db)
        require(server['offline']==0,'Completion retained offline mode')
        persisted=db.scalar("SELECT VARIABLE_VALUE FROM performance_schema.persisted_variables WHERE VARIABLE_NAME='offline_mode'")
        require(persisted=='OFF','Completion removal not persistent')
        api=Probes(db,config).api('CALL er_schema_ready()')
        require(api.returncode==0 and api.stdout.strip()=='1','API readiness failed: '+api.stderr)
        records.append(dict(database=config['database'],server=server,persistedOfflineMode=persisted,
            administrativeTcpIdentity=db.execute('SELECT USER(),CURRENT_USER()'),
            administrativeTcpGrants=db.execute('SHOW GRANTS').splitlines(),apiReadiness=result_record(api)))
    finally:
        db.close()
config=json.loads((base/'upgrade-config.json').read_text(encoding='utf-8-sig'))
config['recoveryBinding']=attested['afterBinding']; config['port']=attested['attestedTcpTarget']['port']
socket=subprocess.run(socket_args(config),input='SELECT VERSION(),@@server_uuid,@@global.offline_mode,USER(),CURRENT_USER();\nSHOW GRANTS;\n',text=True,capture_output=True,timeout=10)
require(socket.returncode==0,'Final socket refused')
(base/'final-identity.json').write_text(json.dumps(dict(status='PASS',targets=records,socket=result_record(socket)),indent=2))
print('PASS: both API readiness calls, persistent barrier OFF, TCP and socket identities/grants')
