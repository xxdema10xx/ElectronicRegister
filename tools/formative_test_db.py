"""Native disposable MySQL 5.7 test runner. No production target, DROP or reset.

Uses installed binaries directly; never changes PowerShell execution policy.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import time
import uuid
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
BIN = Path(r'C:\MAMP\bin\mysql\bin')
DB = 'electronic_register_test'
HIDDEN = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0


def require(value, message):
    if not value:
        raise RuntimeError(message)


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()


class Instance:
    def __init__(self, run):
        self.run = Path(run).resolve(strict=True)
        require((ROOT / '.staff-private') in self.run.parents, 'Private workspace instance required')
        self.identity = json.loads((self.run / 'identity.json').read_text())
        self.port = self.identity['port']
        require(self.port not in (3306, 13306), 'Existing ports forbidden')

    def client(self, sql, database=True, xml=False):
        args = [str(BIN / 'mysql.exe'), '--no-defaults', '--protocol=TCP', '--host=127.0.0.1',
                f'--port={self.port}', '--user=root', '--default-character-set=utf8mb4']
        args += ['--xml'] if xml else ['--batch', '--raw', '--skip-column-names']
        if database: args += ['--database='+DB]
        env = dict(os.environ, MYSQL_PWD=(self.run/'password.txt').read_text())
        p = subprocess.run(args, input=sql.encode('utf-8'), capture_output=True, env=env, creationflags=HIDDEN)
        require(p.returncode == 0, 'SQL failed: '+p.stderr.decode(errors='replace')[:2000])
        return p.stdout.decode('utf-8')

    def rows(self, sql):
        root = ET.fromstring(self.client(sql, xml=True))
        return [{f.attrib['name']: f.text for f in row} for row in root.findall('row')]

    def attest(self):
        row=self.rows('SELECT VERSION() AS version,@@port AS port,@@datadir AS datadir,@@server_uuid AS uuid;')[0]
        require(row['version']=='5.7.24' and int(row['port'])==self.port and
                Path(row['datadir']).resolve()==Path(self.identity['datadir']).resolve() and row['uuid']==self.identity['uuid'], 'Live identity mismatch')
        # PID must belong to this freshly started native process, not merely a matching socket.
        output=subprocess.check_output(['powershell','-NoProfile','-Command',f"Get-Process -Id {int(self.identity['pid'])} | Select-Object Id,ProcessName,Path | ConvertTo-Json -Compress"],creationflags=HIDDEN).decode(errors='replace')
        process=json.loads(output)
        require(process['Id']==self.identity['pid'] and process['ProcessName']=='mysqld' and Path(process['Path']).resolve()==(BIN/'mysqld.exe').resolve(), 'Native PID mismatch')
        return row

    def contract(self):
        tables={}
        for r in self.rows('SHOW TABLES;'):
            name=next(iter(r.values()))
            tables[name]=self.rows(f'SHOW CREATE TABLE `{name}`;')[0]['Create Table']
        metadata={}
        triggers={}
        for r in self.rows('SELECT * FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() ORDER BY TRIGGER_NAME;'):
            r.pop('CREATED',None)  # Installation timestamp is not a schema contract field.
            name=r['TRIGGER_NAME']; metadata[name]=r
            definition=self.rows(f'SHOW CREATE TRIGGER `{name}`;')[0]['SQL Original Statement']
            triggers[name]={'name':name,'statement':definition,
                            'mode':r['SQL_MODE'],'charset':r['CHARACTER_SET_CLIENT'],'collation':r['COLLATION_CONNECTION'],'database_collation':r['DATABASE_COLLATION']}
        fk=int(self.rows("SELECT COUNT(*) AS n FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_TYPE='FOREIGN KEY';")[0]['n'])
        indexes=int(self.rows('SELECT COUNT(*) AS n FROM (SELECT TABLE_NAME,INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() GROUP BY TABLE_NAME,INDEX_NAME) x;')[0]['n'])
        return dict(version='5.7.24',database=DB,charset='utf8mb4',collation='utf8mb4_unicode_ci',
                    inventory=dict(tables=len(tables),triggers=len(metadata),fks=fk,indexes=indexes),tables=tables,triggers=triggers,trigger_metadata=metadata)

    def verify_predecessor(self, actual):
        expected=json.loads((ROOT/'migrations/institution-formative-plan/predecessor-contract.json').read_text(encoding='utf-8-sig'))
        require(actual['inventory']==expected['inventory'],'Predecessor inventory mismatch')
        require(actual['tables']==expected['tables'],'Predecessor table definitions mismatch')
        require(set(actual['trigger_metadata'])==set(expected['trigger_metadata']),'Predecessor triggers mismatch')
        for name,row in actual['trigger_metadata'].items():
            wanted=expected['trigger_metadata'][name]
            for key in row:
                body=wanted[key]
                if key=='ACTION_STATEMENT':
                    body=body.replace('@@port=13306',f'@@port={self.port}').replace("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'",f"LOWER(REPLACE(@@datadir,CHAR(92),'/'))='{Path(self.identity['datadir']).as_posix().lower()}/'")
                require(str(row[key] or '')==str(body or ''),f'Predecessor trigger definition mismatch: {name}/{key}')


def create(port, full):
    require(1024 <= port <=65535 and port not in (3306,13306),'New port required')
    with socket.socket() as probe: require(probe.connect_ex(('127.0.0.1',port))!=0,'Port occupied')
    require(b'5.7.24' in subprocess.check_output([str(BIN/'mysqld.exe'),'--no-defaults','--version'],creationflags=HIDDEN),'Wrong version')
    run=ROOT/'.staff-private'/('formative-'+uuid.uuid4().hex); run.mkdir(parents=True)
    # Reuse the existing private ACL helper; no execution-policy modification.
    from local_test_db import private_acl
    private_acl(run)
    data=run/'data'
    with (run/'init.log').open('wb') as log:
        p=subprocess.run([str(BIN/'mysqld.exe'),'--no-defaults','--initialize-insecure',f'--datadir={data}',f'--basedir={BIN.parent}'],stdout=log,stderr=log,creationflags=HIDDEN)
    require(p.returncode==0,'Initialization failed; preserve logs')
    log=(run/'server.log').open('wb')
    p=subprocess.Popen([str(BIN/'mysqld.exe'),'--no-defaults',f'--datadir={data}',f'--basedir={BIN.parent}',f'--port={port}',
                        '--bind-address=127.0.0.1','--max-connections=200','--explicit-defaults-for-timestamp=1'],stdout=log,stderr=log,creationflags=HIDDEN)
    args=[str(BIN/'mysql.exe'),'--no-defaults','--protocol=TCP','--host=127.0.0.1',f'--port={port}','--user=root','--batch','--raw','--skip-column-names']
    env=dict(os.environ); env.pop('MYSQL_PWD',None)
    for _ in range(100):
        require(p.poll() is None,'Server exited; preserve logs')
        ready=subprocess.run(args,input=b'SELECT VERSION(),@@port,@@datadir,@@server_uuid;',capture_output=True,env=env,creationflags=HIDDEN)
        if ready.returncode==0: break
        time.sleep(.2)
    require(ready.returncode==0,'Readiness failed')
    version,actual_port,actual_data,server_uuid=ready.stdout.decode().strip().split('\t')
    require(version=='5.7.24' and int(actual_port)==port and Path(actual_data).resolve()==data.resolve(),'Fresh identity mismatch')
    password=secrets.token_hex(32); (run/'password.txt').write_text(password)
    provision=f"ALTER USER 'root'@'localhost' IDENTIFIED BY '{password}'; CREATE DATABASE {DB} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE USER 'er_test'@'localhost'; GRANT ALL PRIVILEGES ON {DB}.* TO 'er_test'@'localhost';"
    result=subprocess.run(args,input=provision.encode(),capture_output=True,env=env,creationflags=HIDDEN)
    require(result.returncode==0,'Provision failed')
    identity=dict(version=version,port=port,datadir=str(data),uuid=server_uuid,pid=p.pid,fullBootstrap=full)
    (run/'identity.json').write_text(json.dumps(identity,indent=2))
    inst=Instance(run); inst.attest()
    install(inst,full)
    print(f'New isolated MySQL {version}: port {port}; PID {p.pid}; {run}')


def install(inst,full):
    run=inst.run;data=Path(inst.identity['datadir']);port=inst.port
    require(not inst.rows('SHOW TABLES;'),'Initial bootstrap requires an empty fresh instance')
    if full: sql=(ROOT/'migrations/bootstrap/001_schema.sql').read_text(encoding='utf-8-sig')
    else: sql=subprocess.check_output(['git','show','6133634c32fe013df4f8df8b79e17cd53701b030:migrations/bootstrap/001_schema.sql'],cwd=ROOT).decode('utf-8-sig')
    # Fresh CREATE only: bind root fixture teardown guards to exact disposable identity.
    sql=sql.replace('@@port=13306',f'@@port={port}').replace("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'",f"LOWER(REPLACE(@@datadir,CHAR(92),'/'))='{data.as_posix().lower()}/'")
    (run/'bootstrap-installed.sql').write_text(sql,encoding='utf-8')
    inst.client(sql)
    inst.client("INSERT INTO institution(id,name,code,status) VALUES ('4d543341-0000-4000-8000-000000000001','Technical legacy test holding','MT3A_LEGACY_TEST','provisioning'); INSERT INTO mt3a_legacy_scope(scope,institution_id) VALUES ('legacy','4d543341-0000-4000-8000-000000000001');")
    (ROOT/'.formative-private/latest-instance.txt').write_text(str(run))
    (run/'contract-before.json').write_text(json.dumps(inst.contract(),indent=2),encoding='utf-8')
    print(f'Initial bootstrap installed: {run}')


def probe_sql(inst):
    inst.attest()
    require(inst.contract()['inventory']==dict(tables=60,triggers=153,fks=171,indexes=257),'Full schema required')
    tenant,path,plan,version=[str(uuid.uuid4()) for _ in range(4)]
    inst.client(f"INSERT INTO institution(id,code,name,status) VALUES('{tenant}','F4SQL_{uuid.uuid4().hex}','Synthetic SQL probe','active');"
                f"INSERT INTO study_path(id,institution_id,name) VALUES('{path}','{tenant}','Synthetic SQL probe');"
                f"INSERT INTO formative_plan(id,institution_id,study_path_id,code,name,revision,created_at_utc) VALUES('{plan}','{tenant}','{path}','CodeProbe','Synthetic SQL probe',0,UTC_TIMESTAMP(6));"
                f"INSERT INTO formative_plan_version(id,institution_id,study_path_id,plan_id,version_number,name,status,revision,theory_planned_minutes,internship_planned_minutes,created_at_utc) VALUES('{version}','{tenant}','{path}','{plan}',1,'Synthetic SQL probe','Draft',0,75,0,UTC_TIMESTAMP(6));")
    probes=[('code_case_immutable',f"UPDATE formative_plan SET code='CODEPROBE',revision=revision+1 WHERE id='{plan}';"),
            ('enum_zero_rejected_without_strict_mode',f"SET SESSION sql_mode=''; UPDATE formative_plan_version SET status='NotAState',revision=revision+1 WHERE id='{version}';")]
    results=[]
    for name,sql in probes:
        inst.attest()
        try: inst.client(sql)
        except RuntimeError as error:
            require('ERROR 1644' in str(error),'Unexpected SQL error in '+name)
            results.append(dict(name=name,passed=True,mysqlError=1644))
        else: raise RuntimeError('SQL guard bypassed: '+name)
    row=inst.rows(f"SELECT status,revision FROM formative_plan_version WHERE id='{version}';")[0]
    require(row==dict(status='Draft',revision='0'),'Rejected SQL changed draft state')
    (inst.run/'sql-probes.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    print('Two native SQL hardening probes passed; rejected writes left Draft revision zero.')


def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--create',type=int);parser.add_argument('--full',action='store_true');parser.add_argument('--run');parser.add_argument('--install-empty',action='store_true');parser.add_argument('--apply',action='store_true');parser.add_argument('--capture',action='store_true');parser.add_argument('--probe-sql',action='store_true')
    args=parser.parse_args()
    if args.create: create(args.create,args.full);return
    require(args.run,'--run required');inst=Instance(args.run);inst.attest()
    if args.install_empty: install(inst,args.full)
    if args.apply:
        before=inst.contract();inst.verify_predecessor(before)
        require(inst.rows('SELECT COUNT(*) AS n FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();')[0]['n']=='0','Concurrent DB clients')
        backup=inst.run/'before-migration.sql';require(not backup.exists(),'Backup already exists; no replay')
        env=dict(os.environ,MYSQL_PWD=(inst.run/'password.txt').read_text())
        with backup.open('xb') as output:
            p=subprocess.run([str(BIN/'mysqldump.exe'),'--no-defaults','--protocol=TCP','--host=127.0.0.1',f'--port={inst.port}','--user=root','--single-transaction','--routines','--triggers','--hex-blob',DB],stdout=output,stderr=subprocess.PIPE,env=env,creationflags=HIDDEN)
        require(p.returncode==0 and backup.stat().st_size>0,'Backup failed')
        (inst.run/'backup-sha256.txt').write_text(sha(backup))
        inst.attest();inst.client((ROOT/'migrations/institution-formative-plan/001_expand.sql').read_text())
        after=inst.contract()
        require(all(after['tables'][k]==v for k,v in before['tables'].items()),'Legacy table changed')
        require(all(after['trigger_metadata'][k]==v for k,v in before['trigger_metadata'].items()),'Legacy trigger changed')
        checks=inst.client((ROOT/'migrations/institution-formative-plan/002_validate.sql').read_text())
        (inst.run/'postflight.tsv').write_text(checks)
        require(len(checks.strip().splitlines())==10 and all(line.split('\t')[-1]=='0' for line in checks.strip().splitlines()),'Integrity postflight failed')
        (inst.run/'contract-after.json').write_text(json.dumps(after,indent=2),encoding='utf-8')
        print('Migration succeeded; backup SHA256 saved; predecessor tables/triggers preserved:',after['inventory'])
    if args.capture:
        out=inst.run/'contract-current.json';out.write_text(json.dumps(inst.contract(),indent=2),encoding='utf-8');print(out)
    if args.probe_sql: probe_sql(inst)


if __name__=='__main__': main()
