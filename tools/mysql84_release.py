"""MySQL 8.4.13 release runner. Explicit install/upgrade/recover, never API startup.

Requires a dedicated server and a persistent private journal directory. DDL is
not retried. Recovery reconciles one recorded intent at a time against the
independently derived complete schema, before issuing further DDL.
"""
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import subprocess
import tempfile
import threading
import uuid

ROOT = Path(__file__).resolve().parents[1]
RELEASE = 'er-quality-2-mysql84'


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def sha(value):
    return hashlib.sha256(value.replace('\r\n', '\n').encode()).hexdigest().upper()


def literal(value):
    if str(value) == '':
        return "''"
    return "CONVERT(0x" + str(value).encode().hex() + " USING utf8mb4)"


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix('.pending')
    with temporary.open('w', encoding='utf-8', newline='\n') as stream:
        os.chmod(temporary, 0o600)
        json.dump(value, stream, indent=2)
        stream.write('\n')
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)
    if os.name != 'nt':
        descriptor = os.open(path.parent, os.O_RDONLY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)


class Mysql:
    def __init__(self, config):
        for key in ('database', 'definerUser', 'definerHost', 'apiUser', 'apiHost', 'reviewUser', 'reviewHost'):
            require(re.fullmatch(r'[A-Za-z0-9_.%-]+', config.get(key, '')), 'Unsafe or missing ' + key)
        isolated = config.get('isolatedCI') is True
        socket_mode = config.get('transport') == 'socket'
        if isolated:
            require(os.environ.get('GITHUB_ACTIONS') == 'true' and config['host'] == '127.0.0.1'
                    and (config['database'].startswith('er84_') or config['database'] == 'electronic_register_test') and config['port'] not in (3306, 13306), 'CI target rejected')
        else:
            require(config.get('dedicatedServer') is True and (socket_mode or config.get('caFile')), 'Dedicated server and secure transport required')
        prefix = config.get('mysqlPrefix', [])
        if prefix:
            require(not isolated and len(prefix) == 4 and prefix[:3] == ['docker', 'exec', '-i'] and re.fullmatch(r'[a-f0-9]{64}', prefix[3]), 'Only an explicit local container ID may prefix the client')
            require(os.environ.get('DOCKER_HOST', 'unix:///var/run/docker.sock') == 'unix:///var/run/docker.sock' and os.environ.get('DOCKER_CONTEXT', 'default') == 'default', 'Only local Docker transport accepted')
        args = prefix + [config.get('mysqlClient', 'mysql'), '--defaults-extra-file=' + config['defaultsFile'],
                '--batch', '--raw', '--skip-column-names', '--unbuffered', '--skip-reconnect',
                '--default-character-set=utf8mb4']
        if socket_mode:
            require(not isolated and config.get('socketFile') == '/var/run/mysqld/mysqld.sock', 'Explicit staging socket required')
            args += ['--protocol=SOCKET', '--socket=' + config['socketFile']]
        else:
            args += ['--host=' + config['host'], '--port=' + str(config['port']), '--protocol=TCP']
            args += ['--ssl-mode=DISABLED', '--get-server-public-key'] if isolated else ['--ssl-mode=VERIFY_IDENTITY', '--ssl-ca=' + config['caFile']]
        self.args = args
        self.process = subprocess.Popen(args, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                        stderr=subprocess.PIPE, text=True, encoding='utf-8', bufsize=1)
        self.output = queue.Queue()
        def pump():
            for line in self.process.stdout:
                self.output.put(line.rstrip('\n'))
            self.output.put(None)
        threading.Thread(target=pump, daemon=True).start()
        self.config = config

    def execute(self, sql):
        marker = 'er_' + uuid.uuid4().hex
        delimiter = '$$' + uuid.uuid4().hex
        script = f"DELIMITER {delimiter}\n{sql}\n{delimiter}\nDELIMITER ;\nSELECT '{marker}';\n"
        self.process.stdin.write(script)
        self.process.stdin.flush()
        rows = []
        while True:
            try:
                row = self.output.get(timeout=120)
            except queue.Empty:
                raise RuntimeError('MySQL timeout; keep maintenance enabled and reconcile journal') from None
            if row is None:
                self.process.wait(timeout=5)
                match = re.search(r'ERROR (\d+) \(([A-Z0-9]{5})\)', self.process.stderr.read())
                code = ' errno=' + match[1] + ' sqlstate=' + match[2] if match else ''
                raise RuntimeError('MySQL statement failed;' + code + '; no automatic retry; maintenance must remain enabled')
            if row == marker:
                return '\n'.join(rows)
            rows.append(row)

    def scalar(self, sql):
        return self.execute(sql).strip()

    def json(self, sql):
        return json.loads(self.scalar(sql))

    def close(self):
        if self.process.poll() is None:
            self.process.stdin.close()
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.process.terminate()


def load_contract(modern=True):
    return json.loads(subprocess.check_output(['node', str(ROOT / 'tools/mysql84-release.cjs'), 'contract',
                                              'new' if modern else 'old'], cwd=ROOT, text=True))


def identity(db, config):
    row = db.json("SELECT JSON_OBJECT('version',VERSION(),'uuid',@@server_uuid,'port',@@port,'offline',@@global.offline_mode,'events',@@global.event_scheduler,'mode',@@global.sql_mode)")
    require(row['version'] == '8.4.13' and row['uuid'] == config['serverUuid'] and row['port'] == config.get('serverPort', 3306), 'Server identity mismatch')
    require(db.scalar('SELECT COUNT(*) FROM information_schema.EVENTS') == '0', 'Scheduled events exist on the dedicated release server')
    require(set(row['mode'].split(',')) == {'ONLY_FULL_GROUP_BY', 'STRICT_TRANS_TABLES', 'NO_ZERO_IN_DATE', 'NO_ZERO_DATE', 'ERROR_FOR_DIVISION_BY_ZERO', 'NO_ENGINE_SUBSTITUTION'}, 'Server SQL mode differs from the certified 8.4 mode')
    if not config.get('isolatedCI'):
        schemas = db.json('SELECT JSON_ARRAYAGG(SCHEMA_NAME) FROM information_schema.SCHEMATA')
        require(set(schemas) <= {'mysql', 'information_schema', 'performance_schema', 'sys', 'er_stage_control', config['database']}, 'Other application schemas exist; global maintenance is refused')
        if config['database'] == 'electronic_register_staging':
            require(re.fullmatch(r'[a-f0-9]{32}', config.get('stageId', '')), 'Staging ownership binding required')
            require(db.scalar('SELECT id FROM er_stage_control.identity') == config['stageId'], 'Staging ownership mismatch')
    db.execute('USE `' + config['database'] + '`')
    return row


def admin_identity(db):
    # PROCESS is required to see all sessions, otherwise an apparently empty
    # PROCESSLIST would be an unsafe maintenance attestation.
    grants = db.execute('SHOW GRANTS')
    global_all = 'GRANT ALL PRIVILEGES ON *.*' in grants
    def has(privilege):
        return any(re.search(r'\b' + privilege + r'\b', line.split(' ON ')[0]) and ' ON *.*' in line for line in grants.splitlines())
    require(global_all or (has('PROCESS') and has('SHOW DATABASES') and has('CONNECTION_ADMIN') and has('SYSTEM_VARIABLES_ADMIN')), 'Administrative migration identity requires PROCESS, SHOW DATABASES, CONNECTION_ADMIN and SYSTEM_VARIABLES_ADMIN; API identity is forbidden')


def trigger_rows(db):
    fields = dict(name='TRIGGER_NAME', body='ACTION_STATEMENT', mode='SQL_MODE', definer='DEFINER',
                  charset='CHARACTER_SET_CLIENT', collation='COLLATION_CONNECTION', database_collation='DATABASE_COLLATION',
                  table='EVENT_OBJECT_TABLE', event='EVENT_MANIPULATION', timing='ACTION_TIMING', order='ACTION_ORDER')
    pairs = ','.join("'" + k + "',`" + v + "`" for k, v in fields.items())
    rows = db.json('SELECT COALESCE(JSON_ARRAYAGG(JSON_OBJECT(' + pairs + ')),JSON_ARRAY()) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()')
    return {r.pop('name'): r for r in rows}


def expected_body(trigger):
    return trigger.get('body') or re.split(r' FOR EACH ROW\s+', trigger['statement'], maxsplit=1)[1]


def verify_schema(db, contract, config, absent=()):
    # Never derive expectations from observed data. Full inventory + SHOW CREATE,
    # complete bodies, order, metadata and owner are checked independently.
    owner = literal(config['definerUser'])
    owner_host = literal(config['definerHost'])
    require(db.scalar('SELECT COUNT(*) FROM mysql.user WHERE User=' + owner + ' AND Host=' + owner_host + " AND account_locked='Y' AND plugin='caching_sha2_password'") == '1', 'Definer must exist and remain locked with the 8.4 authentication plugin')
    owner_grants = db.execute("SHOW GRANTS FOR '" + config['definerUser'] + "'@'" + config['definerHost'] + "'").splitlines()
    require(len(owner_grants) == 3 and {g.split(' TO ')[0] for g in owner_grants} == {
        'GRANT USAGE ON *.*', 'GRANT SELECT, INSERT, UPDATE, DELETE, TRIGGER ON `' + config['database'] + '`.*',
        'GRANT EXECUTE ON PROCEDURE `' + config['database'] + '`.`er_schema_ready`'} and all('WITH GRANT OPTION' not in g for g in owner_grants), 'Definer privileges/roles drift')
    api = "'" + config['apiUser'] + "'@'" + config['apiHost'] + "'"
    ssl_type = '' if config.get('isolatedCI') else 'ANY'
    require(db.scalar('SELECT COUNT(*) FROM mysql.user WHERE User=' + literal(config['apiUser']) + ' AND Host=' + literal(config['apiHost']) +
                      " AND account_locked='N' AND plugin='caching_sha2_password' AND ssl_type=" + literal(ssl_type)) == '1', 'API authentication/TLS account properties drift')
    grants = db.execute('SHOW GRANTS FOR ' + api).splitlines()
    prefixes = {g.split(' TO ')[0] for g in grants}
    require(len(grants) == 3 and prefixes == {'GRANT USAGE ON *.*', 'GRANT SELECT, INSERT, UPDATE, DELETE ON `' + config['database'] + '`.*',
             'GRANT EXECUTE ON PROCEDURE `' + config['database'] + '`.`er_schema_ready`'} and all('WITH GRANT OPTION' not in g for g in grants), 'API privileges/roles drift')
    tables = db.json("SELECT COALESCE(JSON_ARRAYAGG(TABLE_NAME),JSON_ARRAY()) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()")
    require(sorted(tables) == sorted(contract['tables']), 'Table inventory drift')
    require(db.scalar('SELECT DEFAULT_COLLATION_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME=DATABASE()') == 'utf8mb4_unicode_ci', 'Database collation drift')
    for name, expected in contract['tables'].items():
        actual = db.execute('SHOW CREATE TABLE `' + name + '`').split('\t', 1)[1]
        actual = re.sub(r' AUTO_INCREMENT=\d+', '', actual)
        actual = re.sub(r"(^  `[^`]+` (?:(?:char|varchar)\(\d+\)|enum\('[^']*'(?:,'[^']*')*\)|tinytext|text|mediumtext|longtext)) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci", r'\1 COLLATE utf8mb4_unicode_ci', actual, flags=re.M)
        actual = actual.replace(' CHARACTER SET ascii COLLATE ascii_general_ci', ' CHARACTER SET ascii')
        require(actual == expected, 'Table DDL drift: ' + name)
    sources = dict(columns=('COLUMNS', 'TABLE_SCHEMA=DATABASE()'), indexes=('STATISTICS', 'TABLE_SCHEMA=DATABASE()'),
                   foreignKeys=('REFERENTIAL_CONSTRAINTS', 'CONSTRAINT_SCHEMA=DATABASE()'),
                   keyColumns=('KEY_COLUMN_USAGE', 'TABLE_SCHEMA=DATABASE() AND REFERENCED_TABLE_NAME IS NOT NULL'))
    for section, (table, where) in sources.items():
        rows = contract['metadata'][section]
        keys = list(rows[0])
        pairs = ','.join("'" + k + "',`" + k + "`" for k in keys)
        names = sorted({r['TABLE_NAME'] for r in rows})
        actual_rows = db.json('SELECT COALESCE(JSON_ARRAYAGG(JSON_OBJECT(' + pairs + ')),JSON_ARRAY()) FROM information_schema.' + table + ' WHERE ' + where + ' AND TABLE_NAME IN (' + ','.join(literal(n) for n in names) + ')')
        canonical = lambda values: sorted(json.dumps(r, sort_keys=True) for r in values)
        require(canonical(actual_rows) == canonical(rows), 'Additive metadata drift: ' + section)
    actual = trigger_rows(db)
    require(sorted(actual) == sorted(set(contract['triggers']) - set(absent)), 'Trigger inventory drift')
    for name, expected in contract['triggers'].items():
        if name in absent:
            continue
        observed = actual[name]
        wanted = {key: value for key, value in expected.items() if key not in ('statement', 'body')}
        wanted['body'] = expected_body(expected)
        wanted['definer'] = config['definerUser'] + '@' + config['definerHost']
        # Dropping one trigger shifts ACTION_ORDER for the later members.
        for missing in absent:
            t = contract['triggers'][missing]
            if all(t[k] == expected[k] for k in ('table', 'event', 'timing')) and t['order'] < expected['order']:
                wanted['order'] -= 1
        require(observed == wanted, 'Trigger metadata/body drift: ' + name)
    routines = db.json("SELECT COALESCE(JSON_ARRAYAGG(JSON_OBJECT('name',ROUTINE_NAME,'type',ROUTINE_TYPE,'body',ROUTINE_DEFINITION,'definer',DEFINER,'mode',SQL_MODE,'security',SECURITY_TYPE,'access',SQL_DATA_ACCESS,'charset',CHARACTER_SET_CLIENT,'collation',COLLATION_CONNECTION,'dbcollation',DATABASE_COLLATION,'deterministic',IS_DETERMINISTIC,'comment',ROUTINE_COMMENT)),JSON_ARRAY()) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE()")
    require(routines == [dict(name='er_schema_ready', type='PROCEDURE', body=contract['routine']['body'],
                              definer=config['definerUser'] + '@' + config['definerHost'], mode=contract['routine']['mode'],
                              security='DEFINER', access='READS SQL DATA', charset='utf8mb4', collation='utf8mb4_unicode_ci',
                              dbcollation='utf8mb4_unicode_ci', deterministic='NO', comment='')], 'Routine drift')
    require(db.scalar("SELECT COUNT(*) FROM information_schema.PARAMETERS WHERE SPECIFIC_SCHEMA=DATABASE()") == '0', 'Unexpected routine parameters')
    # Evaluate the trusted reviewed gate directly, never execute an untrusted body.
    if not absent:
        require(db.scalar(contract['routine']['body'][len('BEGIN\n'):-len('\nEND')].strip()) == '1', 'Readiness gate failed')
    return actual


def fingerprint(db, contract):
    result = {}
    for table in sorted(contract['tables']):
        columns = db.execute("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=" + literal(table) + ' ORDER BY ORDINAL_POSITION').splitlines()
        # Stable byte representation includes NULL and every value; no reliance on
        # auto-increment state or nondeterministic JSON aggregation ordering.
        fields = ','.join('IF(`' + c + '` IS NULL,NULL,HEX(CAST(`' + c + '` AS BINARY)))' for c in columns)
        where = " WHERE version<>'er-quality-2-mysql84'" if table == 'er_schema_release' else ''
        rows = db.execute('SELECT JSON_ARRAY(' + fields + ') FROM `' + table + '`' + where)
        result[table] = sha('\n'.join(sorted(rows.splitlines())))
    return result


class Runner:
    def __init__(self, db, config, journal):
        self.db, self.config, self.path = db, config, Path(journal)
        self.old, self.new = load_contract(False), load_contract(True)
        self.manifest = json.loads((ROOT / 'migrations/mysql84-remediation/manifest.json').read_text())
        self.state = json.loads(self.path.read_text()) if self.path.exists() else None

    def persist(self):
        save(self.path, self.state)

    def lock(self):
        require(self.db.scalar("SELECT GET_LOCK('er-quality-2-mysql84',0)") == '1', 'Another release operation is active')

    def maintenance(self):
        # Persist the maintenance barrier across a server restart after a DROP.
        self.db.execute('SET PERSIST offline_mode=ON')
        self.quiescent()

    def quiescent(self):
        require(self.db.scalar('SELECT @@global.offline_mode') == '1', 'Maintenance barrier lost')
        # Offline mode does not evict CONNECTION_ADMIN/SYSTEM_USER sessions.
        # Refuse all remaining foreground clients except this one. No blind KILL.
        require(self.db.scalar("SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID<>CONNECTION_ID() AND USER NOT IN ('event_scheduler','system user')") == '0', 'Other sessions remain; maintenance stays enabled')
        require(self.db.scalar('SELECT COUNT(*) FROM information_schema.INNODB_TRX WHERE trx_mysql_thread_id<>CONNECTION_ID()') == '0', 'Other transactions remain')
        require(self.db.scalar("SELECT COUNT(*) FROM performance_schema.replication_applier_status WHERE SERVICE_STATE='ON'") == '0', 'Replication applier is active')

    def validate_state(self):
        require(self.state and self.state.get('format') == 1 and self.state['manifest'] == sha(json.dumps(self.manifest, sort_keys=True)) and
                self.state['database'] == self.config['database'] and self.state['serverUuid'] == self.config['serverUuid'], 'Journal identity/checksum mismatch')
        if self.state['operation'] == 'upgrade':
            names = self.manifest['changedTriggers']
            require(list(self.state['triggers']) == names[:len(self.state['triggers'])], 'Journal trigger sequence drift')
            if self.state['phase'] in ('marker-intent', 'verified-maintenance', 'done'):
                require(list(self.state['triggers']) == names and all(r['phase'] == 'done' for r in self.state['triggers'].values()), 'Incomplete terminal journal')
            partial = False
            for name, record in self.state['triggers'].items():
                require(not partial and record['phase'] in ('drop-intent', 'create-intent', 'done') and
                        record['dropHash'] == sha('DROP TRIGGER `' + name + '`') and
                        record['createHash'] == sha(self.plan_create(name)), 'Journal DDL drift')
                partial = record['phase'] != 'done'

    def projected(self):
        expected = copy.deepcopy(self.old)
        for name, record in self.state['triggers'].items():
            if record['phase'] == 'done':
                expected['triggers'][name] = self.new['triggers'][name]
        return expected

    def plan_create(self, name):
        t = self.new['triggers'][name]
        statement = t['statement'].replace('CREATE TRIGGER ', 'CREATE DEFINER=`' + self.config['definerUser'] + '`@`' + self.config['definerHost'] + '` TRIGGER ', 1)
        peers = [(n, p) for n, p in self.new['triggers'].items() if n != name and all(p[k] == t[k] for k in ('table', 'event', 'timing'))]
        later = sorted((p['order'], n) for n, p in peers if p['order'] > t['order'])
        earlier = sorted((p['order'], n) for n, p in peers if p['order'] < t['order'])
        order = (' PRECEDES `' + later[0][1] + '`') if later else (' FOLLOWS `' + earlier[-1][1] + '`') if earlier else ''
        return statement.replace(' FOR EACH ROW', ' FOR EACH ROW' + order, 1)

    def upgrade(self, recover=False, fault=None):
        self.lock()
        server = identity(self.db, self.config)
        admin_identity(self.db)
        if self.state:
            self.validate_state()
            if self.state['phase'] == 'done':
                verify_schema(self.db, self.new, self.config)
                require(self.db.scalar("SELECT COUNT(*) FROM er_schema_release WHERE version='er-quality-2-mysql84'") == '1', 'Release marker absent')
                return
            require(recover, 'Incomplete operation: explicit recover required; no DDL replay')
        else:
            require(not recover and not server['offline'], 'Unexpected maintenance state or missing recovery journal')
            # Exactly the predecessor is required BEFORE any DDL or maintenance.
            verify_schema(self.db, self.old, self.config)
            require(self.db.scalar("SELECT COUNT(*) FROM er_schema_release WHERE version='er-quality-2-mysql84'") == '0', 'Missing journal for installed release')
            self.state = dict(format=1, operation='upgrade', phase='maintenance-intent', database=self.config['database'],
                              serverUuid=self.config['serverUuid'], manifest=sha(json.dumps(self.manifest, sort_keys=True)), triggers={})
            self.persist()
        require(self.state['operation'] == 'upgrade', 'Wrong journal operation')
        self.maintenance()
        if 'fingerprint' not in self.state:
            verify_schema(self.db, self.old, self.config)
            self.state['fingerprint'] = fingerprint(self.db, self.old)
            self.state['phase'] = 'triggers'
            self.persist()
        for name in self.manifest['changedTriggers']:
            record = self.state['triggers'].get(name)
            expected = self.projected()
            if record and record['phase'] == 'done':
                continue
            if record:
                require(recover, 'Partial trigger: explicit recovery required')
                require(record['createHash'] == sha(self.plan_create(name)), 'DDL checksum drift')
                present = trigger_rows(self.db).get(name)
                if present and present['body'] == expected_body(self.new['triggers'][name]):
                    # CREATE committed but acknowledgement was lost. Verify the
                    # ENTIRE projected state before recording done, never replay.
                    expected['triggers'][name] = self.new['triggers'][name]
                    verify_schema(self.db, expected, self.config)
                    record['phase'] = 'done'
                    self.persist()
                    continue
                if present:
                    require(record['phase'] == 'drop-intent', 'Unexpected old trigger after recorded DROP')
                    verify_schema(self.db, expected, self.config)
                else:
                    verify_schema(self.db, expected, self.config, absent=[name])
                    record['phase'] = 'create-intent'
                    self.persist()
            else:
                verify_schema(self.db, expected, self.config)
                record = dict(phase='drop-intent', dropHash=sha('DROP TRIGGER `' + name + '`'), createHash=sha(self.plan_create(name)))
                self.state['triggers'][name] = record
                self.persist()
            if record['phase'] == 'drop-intent':
                require(record['dropHash'] == sha('DROP TRIGGER `' + name + '`'), 'DROP checksum drift')
                self.quiescent()
                self.db.execute('DROP TRIGGER `' + name + '`')
                if fault == name + ':after-drop':
                    raise RuntimeError('Injected interruption after committed DROP')
                record['phase'] = 'create-intent'
                self.persist()
            t = self.new['triggers'][name]
            require('NO_BACKSLASH_ESCAPES' not in t['mode'], 'Unsupported trigger SQL mode')
            self.db.execute('SET SESSION sql_mode=' + literal(t['mode']))
            self.db.execute('SET NAMES ' + t['charset'] + ' COLLATE ' + t['collation'])
            self.quiescent()
            self.db.execute(self.plan_create(name))
            if fault == name + ':after-create':
                raise RuntimeError('Injected interruption after committed CREATE')
            expected['triggers'][name] = t
            verify_schema(self.db, expected, self.config)
            record['phase'] = 'done'
            self.persist()
        verify_schema(self.db, self.new, self.config)
        require(fingerprint(self.db, self.new) == self.state['fingerprint'], 'Data changed during upgrade; keep maintenance enabled')
        # DML is idempotently reconciled, never rewrites existing release history.
        self.state['phase'] = 'marker-intent'
        self.persist()
        self.db.execute("INSERT INTO er_schema_release(version) SELECT 'er-quality-2-mysql84' WHERE NOT EXISTS(SELECT 1 FROM er_schema_release WHERE version='er-quality-2-mysql84')")
        self.state['phase'] = 'verified-maintenance'
        self.persist()
        self.db.execute('SET PERSIST offline_mode=OFF')
        self.state['phase'] = 'done'
        self.persist()


def install(db, config, journal, bundle):
    """Fresh install only; partial batches cannot be replayed automatically."""
    runner = Runner(db, config, journal)
    runner.lock()
    server = identity(db, config)
    admin_identity(db)
    binding = json.loads((Path(bundle) / 'binding.json').read_text())
    require(binding['release'] == RELEASE and binding['database'] == config['database'] and
            binding['definer'] == config['definerUser'] + '@' + config['definerHost'], 'Bundle binding mismatch')
    files = {p.name: sha(p.read_text()) for p in Path(bundle).glob('*.sql')}
    require(files == binding['files'], 'Bundle checksum/inventory mismatch')
    # Checksums supplied by the bundle itself are not an authority. Rebuild the
    # expected environment binding from the independently verified release.
    with tempfile.TemporaryDirectory(prefix='er84-expected-') as temporary:
        cfg = Path(temporary) / 'config.json'
        cfg.write_text(json.dumps(config))
        expected_dir = Path(temporary) / 'bundle'
        subprocess.run(['node', str(ROOT / 'tools/mysql84-release.cjs'), 'bundle', str(cfg), str(expected_dir)], check=True)
        require(binding == json.loads((expected_dir/'binding.json').read_text()), 'Untrusted derived bundle binding')
    if runner.state:
        runner.validate_state()
        require(runner.state['operation'] == 'install' and runner.state['bundle'] == sha(json.dumps(binding, sort_keys=True)), 'Install journal drift')
        if runner.state['phase'] == 'done':
            verify_schema(db, runner.new, config)
            return
        raise RuntimeError('Partial fresh install: inspect journal and isolated schema; no automatic replay')
    require(not server['offline'], 'Unowned maintenance state before fresh install')
    require(db.scalar('SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()') == '0', 'Fresh install requires empty database')
    runner.state = dict(format=1, operation='install', phase='maintenance-intent', database=config['database'],
                        serverUuid=config['serverUuid'], manifest=sha(json.dumps(runner.manifest, sort_keys=True)),
                        bundle=sha(json.dumps(binding, sort_keys=True)), files={})
    runner.persist()
    runner.maintenance()
    for name in sorted(files):
        runner.state['files'][name] = dict(hash=files[name], phase='started')
        runner.persist()
        # This script already contains reviewed DELIMITER commands.
        marker = 'er_' + uuid.uuid4().hex
        db.process.stdin.write((Path(bundle) / name).read_text() + f"\nSELECT '{marker}';\n")
        db.process.stdin.flush()
        while True:
            row = db.output.get(timeout=120)
            require(row is not None, 'Fresh SQL failed; no replay')
            if row == marker:
                break
        runner.state['files'][name]['phase'] = 'done'
        runner.persist()
    verify_schema(db, runner.new, config)
    runner.state['phase'] = 'verified-maintenance'
    runner.persist()
    db.execute('SET PERSIST offline_mode=OFF')
    runner.state['phase'] = 'done'
    runner.persist()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['preflight', 'predecessor-preflight', 'install', 'upgrade', 'recover'])
    parser.add_argument('--config', required=True)
    parser.add_argument('--journal')
    parser.add_argument('--bundle')
    args = parser.parse_args()
    subprocess.run(['node', str(ROOT / 'tools/mysql84-release.cjs')], check=True, cwd=ROOT)
    config = json.loads(Path(args.config).read_text(encoding='utf-8-sig'))
    db = Mysql(config)
    try:
        identity(db, config)
        if args.action.endswith('preflight'):
            verify_schema(db, load_contract(args.action == 'preflight'), config)
            if args.action == 'preflight':
                require(db.scalar("SELECT COUNT(*) FROM er_schema_release WHERE version='er-quality-2-mysql84'") == '1', 'New release marker missing')
            else:
                require(db.scalar("SELECT COUNT(*) FROM er_schema_release WHERE version='er-quality-2-mysql84'") == '0', 'Predecessor has an unexpected successor marker')
        else:
            require(args.journal, 'Persistent private journal path required')
            if args.action == 'install':
                require(args.bundle, 'Bound fresh bundle required')
                install(db, config, args.journal, args.bundle)
            else:
                Runner(db, config, args.journal).upgrade(recover=args.action == 'recover')
        print(json.dumps(dict(release=RELEASE, action=args.action, verified=True)))
    finally:
        db.close()


if __name__ == '__main__':
    main()
