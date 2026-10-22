"""Windows MySQL 5.7.24 test-only bootstrap. Standard library; no private repo inputs.

Never points at 3306, deletes a database, disables constraints or repeats a stage.
DDL failures deliberately leave the partial instance and journal for review.
"""
import argparse
import getpass
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
DB = 'electronic_register_test'
SUFFIX = '/migrations/mt4/private/mt5a-mamp-13306/data'
MODE = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION'

def require(value, message):
    if not value:
        raise RuntimeError(message)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def write_new(path, value):
    with path.open('x', encoding='utf-8', newline='\n') as file:
        json.dump(value, file, indent=2)
        file.write('\n')

def normalize_ddl(value):
    return re.sub(r' AUTO_INCREMENT=\d+', '', value)

def validate_target(port, datadir, main):
    require(port not in (0, 3306) and 1024 <= port <= 65535, 'A dedicated non-3306 port is required')
    require(datadir.as_posix().lower().endswith(SUFFIX), 'Non-canonical isolated datadir')
    require(datadir != main and main not in datadir.parents and datadir not in main.parents, 'Datadir overlaps main instance')
    require(not any(p.is_symlink() or (hasattr(p, 'is_junction') and p.is_junction()) for p in [datadir, *datadir.parents]), 'Reparse points are not supported')

def private_acl(path):
    env = dict(os.environ, ER_SETUP_PRIVATE_PATH=str(path))
    code = """$ErrorActionPreference='Stop'; $p=$env:ER_SETUP_PRIVATE_PATH;
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User;
$a=New-Object Security.AccessControl.DirectorySecurity;
$a.SetAccessRuleProtection($true,$false);
foreach($id in @($sid.Value,'S-1-5-18','S-1-5-32-544')){
 $s=New-Object Security.Principal.SecurityIdentifier($id);
 $r=New-Object Security.AccessControl.FileSystemAccessRule($s,'FullControl','ContainerInherit,ObjectInherit','None','Allow'); $a.AddAccessRule($r)};
Set-Acl -LiteralPath $p -AclObject $a;
$a=Get-Acl -LiteralPath $p; if(!$a.AreAccessRulesProtected){throw 'ACL not private'};
foreach($r in $a.Access){if($r.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -notin @($sid.Value,'S-1-5-18','S-1-5-32-544')){throw 'Unexpected ACL'}}
"""
    p = subprocess.run(['powershell', '-NoProfile', '-Command', code], env=env, capture_output=True)
    require(p.returncode == 0, 'Private ACL setup/verification failed')

def listener(port):
    value = subprocess.check_output(['netstat', '-ano', '-p', 'tcp']).decode(errors='replace')
    return [line.split() for line in value.splitlines() if re.match(r'^\s*TCP\s+\S+:'+str(port)+r'\s+\S+\s+LISTENING\s+\d+\s*$', line)]

def verify_files():
    manifest = json.loads((ROOT/'migrations/bootstrap/manifest.json').read_text(encoding='utf-8'))
    require(manifest['format'] == 1, 'Unknown manifest')
    for relative, sha in manifest['files'].items():
        path = (ROOT/relative).resolve(strict=True)
        require(ROOT in path.parents and digest(path) == sha, 'Source SHA256 mismatch: '+relative)
    return manifest

class Setup:
    def __init__(self, args, password):
        self.args, self.password = args, password
        self.bin = Path(args.mysql_bin).resolve(strict=True)
        self.data = Path(args.datadir).absolute().resolve()
        self.home = self.data.parent
        self.port = args.port
        validate_target(self.port, self.data, Path(args.main_datadir).resolve())
        for exe in ['mysqld.exe', 'mysql.exe', 'mysqladmin.exe', 'mysqldump.exe']:
            require((self.bin/exe).is_file(), 'Missing MySQL executable')
        version = subprocess.check_output([str(self.bin/'mysqld.exe'), '--no-defaults', '--version']).decode()
        require(re.search(r'\b5\.7\.24\b', version), 'Only MySQL 5.7.24 is certified')
        require(bool(password) and '\x00' not in password and '\n' not in password, 'Private password required')

    def client(self, sql, memory=None, password=None, expired=False):
        env = dict(os.environ, MYSQL_PWD=self.password if password is None else password)
        args = [str(self.bin/'mysql.exe'), '--no-defaults', '--user=root', '--connect-timeout=5', '--default-character-set=utf8mb4', '--batch', '--raw', '--skip-column-names']
        args += ['--protocol=MEMORY', '--shared-memory-base-name='+memory] if memory else ['--protocol=TCP', '--host=127.0.0.1', '--port='+str(self.port)]
        if expired:
            args.append('--connect-expired-password')
        p = subprocess.run(args, input=sql.encode('utf-8'), env=env, capture_output=True, timeout=120)
        if p.returncode:
            # Private diagnostics only: SQL errors can contain literals.
            with (self.home/('client-error-'+secrets.token_hex(6)+'.log')).open('xb') as file:
                file.write(p.stderr)
            raise RuntimeError('SQL failed; stop without retry/restore; see private diagnostics')
        return p.stdout.decode('utf-8').replace('\r\n', '\n').strip()

    def query(self, sql):
        return self.client('USE '+DB+';\n'+sql)

    def start(self, extra):
        # Hidden child process, no service installation and no shell interpolation.
        log = (self.home/('process-'+secrets.token_hex(4)+'.log')).open('xb')
        return subprocess.Popen([str(self.bin/'mysqld.exe'), '--defaults-file='+str(self.home/'my.ini'), *extra], stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)

    def wait_ready(self, process, memory=None, password=None):
        deadline = time.monotonic()+60
        while time.monotonic() < deadline:
            require(process.poll() is None, 'Server exited; preserve private error logs')
            try:
                return self.client('SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),\'/\'),@@skip_networking;', memory, password)
            except RuntimeError:
                time.sleep(.5)
        raise RuntimeError('Server readiness timeout; no automatic restart')

    def create(self):
        require(self.args.confirm_isolated_test, 'Explicit isolated-test confirmation required')
        require(not self.home.exists(), 'Instance directory already exists: preserve it; no initialization/replay')
        require(not listener(self.port), 'Port occupied; do not stop that process')
        require(shutil.disk_usage(ROOT).free > 2*1024**3, 'Need at least 2 GiB free')
        self.home.mkdir(parents=True)
        private_acl(self.home)
        write_new(self.home/'initialization.started.json', {'port':self.port,'datadir':str(self.data)})
        (self.home/'tmp').mkdir()
        config = f'''[mysqld]
basedir={self.bin.parent.as_posix()}
datadir={self.data.as_posix()}
port={self.port}
bind-address=127.0.0.1
pid-file={self.home.as_posix()}/mysql.pid
log-error={self.home.as_posix()}/mysql-error.log
tmpdir={self.home.as_posix()}/tmp
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci
sql-mode={MODE}
general-log=0
slow-query-log=0
innodb-buffer-pool-size=64M
innodb-log-file-size=16M
max-allowed-packet=64M
'''
        (self.home/'my.ini').write_text(config, encoding='utf-8', newline='\n')
        p = subprocess.run([str(self.bin/'mysqld.exe'), '--defaults-file='+str(self.home/'my.ini'), '--initialize-insecure', '--skip-networking', '--console'], capture_output=True, timeout=90)
        (self.home/'initialization-private.log').write_bytes(p.stdout+p.stderr)
        require(p.returncode == 0, 'Initialization failed: no retry')
        memory = 'ER_SETUP_'+secrets.token_hex(16)
        proc = self.start(['--skip-networking', '--shared-memory', '--shared-memory-base-name='+memory])
        value = self.wait_ready(proc, memory, '')
        require(not listener(self.port), 'Unexpected TCP listener during password setup')
        require(value.split('\t')[2].rstrip('/').lower() == self.data.as_posix().lower() and value.endswith('\t1'), 'Wrong offline identity')
        # Backslash-free hex literal avoids SQL-mode dependent password escaping.
        password_hex = self.password.encode().hex()
        self.client("SET @p=CONVERT(0x"+password_hex+" USING utf8mb4); SET @s=CONCAT('ALTER USER ''root''@''localhost'' IDENTIFIED BY ',QUOTE(@p)); PREPARE p FROM @s; EXECUTE p; DEALLOCATE PREPARE p;", memory, '')
        self.client("CREATE USER 'er_test'@'localhost' IDENTIFIED BY '"+secrets.token_hex(32)+"' ACCOUNT LOCK; GRANT ALL PRIVILEGES ON electronic_register_test.* TO 'er_test'@'localhost';", memory)
        env = dict(os.environ, MYSQL_PWD=self.password)
        result = subprocess.run([str(self.bin/'mysqladmin.exe'), '--no-defaults', '--user=root', '--protocol=MEMORY', '--shared-memory-base-name='+memory, '--shutdown-timeout=20', 'shutdown'], env=env, capture_output=True, timeout=30)
        require(result.returncode == 0, 'Offline graceful shutdown failed; stop here')
        proc.wait(timeout=30)
        require(proc.returncode == 0 and not listener(self.port), 'Offline process did not terminate cleanly')
        proc = self.start([])
        self.wait_ready(proc)
        uuid = self.client('SELECT @@server_uuid;')
        write_new(self.home/'instance.json', {'kind':'ElectronicRegister isolated test','port':self.port,'datadir':self.data.as_posix(),'mysql_bin':str(self.bin),'hostname':self.client('SELECT @@hostname;'),'server_uuid':uuid,'pid':proc.pid})
        self.identity()
        print('New isolated instance running; private root authentication verified; no blank TCP administrator')

    def identity(self):
        require((self.home/'instance.json').is_file(), 'Missing private creation attestation; existing external servers are refused')
        marker = json.loads((self.home/'instance.json').read_text())
        rows = listener(self.port)
        require(len(rows)==1 and rows[0][1]=='127.0.0.1:'+str(self.port), 'Listener not unique/loopback')
        pid = int(rows[0][-1])
        require(pid==marker['pid'], 'Server PID changed; manual review required')
        env = dict(os.environ, ER_SETUP_PID=str(pid))
        p = subprocess.run(['powershell','-NoProfile','-Command',"$ErrorActionPreference='Stop'; (Get-Process -Id ([int]$env:ER_SETUP_PID)).Path"], env=env, capture_output=True, check=True)
        require(Path(p.stdout.decode().strip()).resolve()==self.bin/'mysqld.exe', 'Wrong listener executable')
        row = self.client("SELECT VERSION(),@@hostname,@@port,REPLACE(@@datadir,CHAR(92),'/'),@@server_uuid,@@GLOBAL.sql_mode,@@bind_address;").split('\t')
        require(row==['5.7.24',marker['hostname'],str(self.port),self.data.as_posix()+'/',marker['server_uuid'],MODE,'127.0.0.1'], 'SQL identity/configuration mismatch')
        require(marker['kind']=='ElectronicRegister isolated test' and marker['port']==self.port and marker['datadir']==self.data.as_posix() and marker['mysql_bin']==str(self.bin), 'Attestation mismatch')
        return row

    def quiet(self):
        require(self.client('SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID();')=='0', 'Other clients present; pause writers first')
        require(self.client('SELECT COUNT(*) FROM information_schema.INNODB_TRX;')=='0', 'Concurrent transactions')

    def bootstrap(self):
        require(self.args.confirm_isolated_test, 'Explicit isolated-test confirmation required')
        manifest = verify_files()
        self.identity(); self.quiet()
        require(not (self.home/'bootstrap.started.json').exists(), 'Bootstrap already attempted; no automatic replay')
        # All non-system databases must be absent or the exact empty target.
        names = self.client("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME NOT IN ('mysql','sys','information_schema','performance_schema');").splitlines()
        require(not names or names==[DB], 'Unexpected database on isolated server')
        exists = DB in names
        require(self.client("SELECT (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+DB+"')+(SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA='"+DB+"')+(SELECT COUNT(*) FROM information_schema.EVENTS WHERE EVENT_SCHEMA='"+DB+"');")=='0', 'Target is not empty')
        if exists:
            require(self.client("SELECT CONCAT(DEFAULT_CHARACTER_SET_NAME,':',DEFAULT_COLLATION_NAME) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+DB+"';")=='utf8mb4:utf8mb4_unicode_ci','Wrong empty database collation')
        require(self.client("SELECT COUNT(*) FROM mysql.user WHERE (User='root' AND Host='localhost') OR (User='er_test' AND Host='localhost' AND account_locked='Y');")=='2','Required definers unavailable')
        write_new(self.home/'bootstrap.started.json', {'manifest_sha256':digest(ROOT/'migrations/bootstrap/manifest.json'),'empty_target':True,'time':time.time()})
        if not exists:
            self.client('CREATE DATABASE '+DB+' CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;')
        # No data exists to back up: journal records the verified empty inventory.
        self.identity(); self.quiet(); verify_files()
        schema = ROOT/'migrations/bootstrap/001_schema.sql'
        self.query(schema.read_text(encoding='utf-8'))  # native mysql handles DELIMITER; never --force
        self.verify()
        # Runtime-generated technical manifest, not exported application/fixture data.
        # Legacy writers require this reserved, non-active holding and its singleton.
        # No User, membership, admin, invitation or audit event is invented.
        self.identity(); self.quiet()
        self.query("INSERT INTO institution(id,name,code,status) VALUES ('4d543341-0000-4000-8000-000000000001','Technical legacy test holding','MT3A_LEGACY_TEST','provisioning'); INSERT INTO mt3a_legacy_scope(scope,institution_id) VALUES ('legacy','4d543341-0000-4000-8000-000000000001');")
        self.verify()
        write_new(self.home/'bootstrap.completed.json', {'manifest_sha256':digest(ROOT/'migrations/bootstrap/manifest.json'),'inventory':manifest['inventory']})

    def verify(self):
        verify_files(); self.identity(); self.quiet()
        expected = json.loads((ROOT/'migrations/bootstrap/schema-contract.json').read_text(encoding='utf-8'))
        require(self.query('SELECT DATABASE();')==DB, 'Wrong selected database')
        require(self.query("SELECT CONCAT(DEFAULT_CHARACTER_SET_NAME,':',DEFAULT_COLLATION_NAME) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME=DATABASE();")=='utf8mb4:utf8mb4_unicode_ci','Database collation mismatch')
        names = self.query('SHOW FULL TABLES;').splitlines()
        inventory=expected['inventory']
        require(len(names)==inventory['tables'] and all(x.endswith('\tBASE TABLE') for x in names), 'Wrong tables/views')
        require(self.query("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='exam_submission' AND COLUMN_NAME='display_order_json' AND DATA_TYPE='longtext' AND IS_NULLABLE='YES';")=='1', 'Submission display-order contract mismatch')
        require(self.query("SELECT COUNT(*) FROM exam_submission WHERE display_order_json IS NOT NULL AND NOT JSON_VALID(display_order_json);")=='0', 'Invalid persisted submission display order')
        class_checks = self.query((ROOT/'migrations/institution-class/002_validate.sql').read_text(encoding='utf-8')).splitlines()
        require(len(class_checks)>=3 and all(row.split('\t')[-1]=='0' for row in class_checks[:3]), 'Class audit validation failed')
        administration_checks = self.query((ROOT/'migrations/institution-administration/002_validate.sql').read_text(encoding='utf-8')).splitlines()
        require(len(administration_checks)==4 and all(row.split('\t')[-1]=='0' for row in administration_checks), 'Institution administration audit validation failed')
        for table, ddl in expected['tables'].items():
            actual = self.query('SHOW CREATE TABLE `'+table+'`;').split('\t',1)[1]
            require(normalize_ddl(actual)==ddl, 'Table/column/index/FK definition mismatch: '+table)
        fields = ['TRIGGER_NAME','EVENT_OBJECT_TABLE','ACTION_TIMING','EVENT_MANIPULATION','ACTION_ORDER','DEFINER','SQL_MODE','CHARACTER_SET_CLIENT','COLLATION_CONNECTION','DATABASE_COLLATION']
        actual = self.query('SELECT '+','.join(fields)+' FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() ORDER BY TRIGGER_NAME;').splitlines()
        require(len(actual)==inventory['triggers'], 'Wrong trigger count')
        for row in actual:
            values = row.split('\t'); name = values[0]; info = expected['trigger_metadata'][name]
            require(values==[str(info[f]) for f in fields], 'Trigger metadata/order mismatch: '+name)
            raw = self.query('SHOW CREATE TRIGGER `'+name+'`;')
            trigger, mode, tail = raw.split('\t',2)
            statement, charset, collation, dbcollation, created = tail.rsplit('\t',4)
            t = expected['triggers'][name]
            require(dict(name=trigger,mode=mode,statement=statement,charset=charset,collation=collation,database_collation=dbcollation)==t, 'Full trigger definition mismatch: '+name)
        # Every FK, including composite relationships, checked with SQL on the real server.
        fkrows = self.query("SELECT TABLE_NAME,CONSTRAINT_NAME,COLUMN_NAME,REFERENCED_TABLE_NAME,REFERENCED_COLUMN_NAME FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME IS NOT NULL ORDER BY TABLE_NAME,CONSTRAINT_NAME,ORDINAL_POSITION;").splitlines()
        groups = {}
        for row in fkrows:
            table, name, col, parent, ref = row.split('\t')
            groups.setdefault((table,name,parent),[]).append((col,ref))
        require(len(groups)==inventory['fks'],'Wrong FK count')
        for (table,name,parent), columns in groups.items():
            on=' AND '.join('c.`'+a+'`=p.`'+b+'`' for a,b in columns)
            notnull=' AND '.join('c.`'+a+'` IS NOT NULL' for a,b in columns)
            require(self.query('SELECT COUNT(*) FROM `'+table+'` c LEFT JOIN `'+parent+'` p ON '+on+' WHERE '+notnull+' AND p.`'+columns[0][1]+'` IS NULL;')=='0','FK orphan: '+name)
        checks = [('migrations/mt4/003_validate.sql',7),('migrations/mt4/006_enrollment_audit_validate.sql',5),('migrations/mt5/candidate-r3/003_security_state_validate.sql',6),('migrations/mt5/candidate-r3/005_delete_contract_validate.sql',2),('migrations/mt5/iam-d/002_validate.sql',4),('migrations/mt5/iam-d/004_validate_snapshot.sql',2)]
        for relative, count in checks:
            values = self.query((ROOT/relative).read_text(encoding='utf-8')).splitlines()
            require(len(values)==count and all(len(x.split('\t'))==2 and x.split('\t')[1]=='0' for x in values),'Validator failed: '+relative)
        if (ROOT/'migrations/mt5/onboarding/002_validate.sql').exists():
            values=self.query((ROOT/'migrations/mt5/onboarding/002_validate.sql').read_text(encoding='utf-8')).splitlines()
            require(len(values)==5 and all(x.split('\t')[1]=='0' for x in values),'Onboarding validator failed')
        if (ROOT/'migrations/mt5/governance/002_validate.sql').exists():
            values=self.query((ROOT/'migrations/mt5/governance/002_validate.sql').read_text(encoding='utf-8')).splitlines()
            require(len(values)==5 and all(x.split('\t')[1]=='0' for x in values),'Governance validator failed')
        require(self.client('SELECT @@GLOBAL.sql_mode;')==MODE,'Global mode changed')
        print('Verified full schema/trigger contexts and all FK without orphans: '+json.dumps(inventory))

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mysql-bin',required=True)
    parser.add_argument('--datadir',default=str(ROOT/'migrations/mt4/private/mt5a-mamp-13306/data'))
    parser.add_argument('--main-datadir',default='C:/MAMP/db/mysql')
    parser.add_argument('--port',type=int,default=13306)
    parser.add_argument('--confirm-isolated-test',action='store_true')
    parser.add_argument('--create-instance',action='store_true')
    parser.add_argument('--verify-only',action='store_true')
    args=parser.parse_args()
    require(os.name=='nt','Windows supported bootstrap only')
    verify_files()
    password=os.environ.get('ER_TEST_DB_PASSWORD') or getpass.getpass('Private root password: ')
    s=Setup(args,password)
    if args.create_instance:
        require(not args.verify_only,'Incompatible modes')
        s.create()
    if args.verify_only:
        s.verify()
    else:
        s.bootstrap()

if __name__=='__main__':
    try:
        main()
    except (RuntimeError,subprocess.SubprocessError,OSError) as exc:
        print('BLOCKED: '+str(exc),file=sys.stderr)
        sys.exit(1)
