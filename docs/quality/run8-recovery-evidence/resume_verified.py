"""Explicit reconciliation after the read-only Docker observer timeout.

Preserves the failed runtime report and journal; never rewrites its outcome.
"""
import json,subprocess,sys
from pathlib import Path
sys.path.insert(0,str(Path('tools').resolve()))
from mysql84_release import Mysql,Runner,identity,load_contract,verify_schema,fingerprint,require
from mysql84_recovery import resume_channel,result_record
from mysql84_runtime import Probes,fresh_install
from runtime_diagnostic import RuntimeDiagnostic

base=Path('.diagnostics/run8-local')
previous=base/'upgrade-verified'
output=base/'recovery-verified'
output.mkdir(exist_ok=False)
config=json.loads((base/'verified-config.json').read_text(encoding='utf-8-sig'))
attested=json.loads((base/'final-restart-regression.json').read_text())['config']
require(attested['mysqlContainer']==config['mysqlContainer'] and attested['serverUuid']==config['serverUuid'],'Attestation mismatch')
config['recoveryBinding']=attested['recoveryBinding']
config['acceptanceEvidenceFile']=str(output/'upgrade-acceptance.json')
journal=previous/'upgrade-journal.json'
saved=journal.read_bytes()
before=json.loads((previous/'before.json').read_text())
diagnostic=RuntimeDiagnostic(output/'runtime-phases.json',['persistent-state-and-authorized-channel','remaining-ddl-recovery','post-completion-acceptance','fresh-install-after-recovery'])
db=None
try:
    with diagnostic.phase('persistent-state-and-authorized-channel'):
        tcp=Mysql(config)
        args=tcp.tcp_args
        tcp.close()
        config=resume_channel(config,args,output/'restart-result.json')
        require(journal.read_bytes()==saved,'Channel verification changed journal')
        db=Mysql(config)
        identity(db,config)
        probes=Probes(db,config)
        blocked=probes.api('SELECT 1')
        (output/'api-barrier-result.json').write_text(json.dumps(result_record(blocked),indent=2))
        require(blocked.returncode!=0 and 'ERROR 3032 (HY000)' in blocked.stderr,'API barrier lost')
    with diagnostic.phase('remaining-ddl-recovery'):
        for fault in ('email_event_insert:after-drop','fpe_insert:after-create','routine:after-drop','routine:after-create'):
            try:
                Runner(db,config,journal).upgrade(recover=True,fault=fault)
                raise AssertionError('Fault boundary not reached')
            except RuntimeError as error:
                require('Injected interruption' in str(error),'Unexpected recovery failure: '+str(error))
            require(db.scalar('SELECT @@global.offline_mode')=='1','Barrier lost')
            db.close()
            db=Mysql(config)
            identity(db,config)
            saved=journal.read_bytes()
            try:
                Runner(db,config,journal).upgrade()
                raise AssertionError('Blind replay accepted')
            except RuntimeError as error:
                require('explicit recover required; no DDL replay' in str(error),'Wrong rejection')
                require(journal.read_bytes()==saved,'Refused replay changed journal')
        Runner(db,config,journal).upgrade(recover=True)
        db.close()
        config.pop('recoverySocket')
        db=Mysql(config)
        identity(db,config)
        require(db.scalar('SELECT @@global.offline_mode')=='0','Barrier not removed')
        require(db.scalar("SELECT VARIABLE_VALUE FROM performance_schema.persisted_variables WHERE VARIABLE_NAME='offline_mode'")=='OFF','Barrier removal not persistent')
        require(fingerprint(db,load_contract())==before,'Existing data changed')
        saved=journal.read_bytes()
        Runner(db,config,journal).upgrade()
        require(saved==journal.read_bytes(),'Completed upgrade replayed')
        guard='ck_er84_institution_invitation_token_hash'
        db.execute('ALTER TABLE institution_invitation ALTER CHECK `'+guard+'` NOT ENFORCED')
        try:
            try:
                Runner(db,config,journal).upgrade()
                raise AssertionError('Disabled CHECK accepted')
            except RuntimeError:
                require(saved==journal.read_bytes(),'Drift changed completed journal')
                body=load_contract()['routine']['body']
                require(db.scalar(body[len('BEGIN\n'):-len('\nEND')].strip())=='0','Readiness accepted disabled CHECK')
        finally:
            db.execute('ALTER TABLE institution_invitation ALTER CHECK `'+guard+'` ENFORCED')
        verify_schema(db,load_contract(),config)
    with diagnostic.phase('post-completion-acceptance'):
        probes=Probes(db,config)
        probes.seed()
        acceptance=probes.all()
        verify_schema(db,load_contract(),config)
        (output/'runtime-result.json').write_text(json.dumps(dict(upgrade=True,explicitRecoveryAfterObserverTimeout=True,
            interruptedDropRecovered=True,lostCreateAckRecovered=True,dataPreserved=True,idempotent=True,
            persistedMaintenanceAcrossServerRestart=True,barrierRemoved=True,acceptance=acceptance),indent=2))
    with diagnostic.phase('fresh-install-after-recovery'):
        fresh_install(db,config,output)
finally:
    if db:db.close()
