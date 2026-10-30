"""Read-only, bounded staging evidence. Never emit raw Docker inspect/config/env."""
import json
import os
import re
import subprocess

SQL_STEPS={'00-login-users','00-principals.sql','01-001_schema.sql',
           '02-001_identity_account.sql','03-002_profile_acquisition.sql',
           '04-003_password_attempt.sql','05-004_release_schema.sql',
           '06-005_release_gate.sql','99-routine-grants.sql','synthetic-seed'}
PHASES={'precheck','bundle-generation','account-creation','migrations','preflight'}

CHECKS={'configuration':'configuration','database-identity':'identity','contract-load':'contract',
        'base-table-ddl':'table','additive-table-ddl':'table','base-trigger-mode':'trigger',
        'base-trigger-charset':'trigger','base-trigger-ddl':'trigger','trigger-inventory':'trigger',
        'additive-trigger-metadata':'trigger','additive-trigger-ddl':'trigger',
        'routine-metadata':'routine','routine-body':'routine','routine-definer':'definer',
        'trigger-definer':'definer','definer-account':'definer','readiness-probe':'metadata'}

def preflight_check(value):
    from pathlib import Path
    if not isinstance(value,dict) or not isinstance(value.get('id'),str) or CHECKS.get(value['id'])!=value.get('category'):return None
    root=Path(__file__).resolve().parents[2]
    base=json.loads((root/'migrations/bootstrap/schema-contract.json').read_text())
    additive=json.loads((root/'migrations/release/additive-contract.json').read_text())
    triggers=set(base['triggers'])|{t['TRIGGER_NAME'] for t in additive['triggers']}
    objects={'configuration':{'release'},'identity':{'database'},'contract':{'release'},
             'table':set(base['tables'])|set(additive['tables']), 'trigger':triggers|{'inventory'},
             'routine':{'er_schema_ready'},'definer':triggers|{'er_schema_ready','schema-owner'},
             'metadata':{'er_schema_ready'}}
    if not isinstance(value.get('object'),str) or value['object'] not in objects[value['category']]:return None
    result={k:value[k] for k in ('id','category','object')}
    if value.get('difference') in ('ddl','body') and all(re.fullmatch(r'[A-F0-9]{64}',str(value.get(k))) for k in ('expectedHash','actualHash')):
        result.update({k:value[k] for k in ('difference','expectedHash','actualHash')})
    return result

def provisioning_evidence(folder):
    """Allowlist projection, including when private state is malformed or hostile."""
    result={}
    for name in ('operation-failure.json','journal.json'):
        path=folder/'ops'/name
        if not path.exists():continue
        try:
            if path.stat().st_size>1024*1024:raise ValueError()
            value=json.loads(path.read_text())
            if not isinstance(value,dict):raise ValueError()
            if name=='journal.json':
                result['startedSteps']=sorted(k for k,v in value.items() if k in SQL_STEPS and isinstance(v,dict) and v.get('state')=='started')
                continue
            result['failure']={
                'phase':value.get('phase') if value.get('phase') in PHASES else None,
                'step':value.get('step') if value.get('step') in SQL_STEPS or re.fullmatch(r'preflight-query-[0-9]{1,5}',str(value.get('step'))) else None,
                **{k:value.get(k) if type(value.get(k)) is int and -2147483648<=value[k]<=2147483647 else None for k in ('exitCode','mysqlErrno','line')},
                'sqlstate':value.get('sqlstate') if re.fullmatch(r'[A-Z0-9]{5}',str(value.get('sqlstate'))) else None}
            check=preflight_check(value.get('check'))
            if check:result['failure']['check']=check
        except (OSError,ValueError,TypeError):result[name]={'unavailable':True}
    return result


def sanitizer(folder):
    # Read all generated credentials BEFORE collecting anything. A read error
    # aborts collection, rather than publishing output with incomplete masking.
    values = set()
    for base in ('secrets', 'pki'):
        for path in (folder / base).rglob('*'):
            if path.is_file():
                content = path.read_text().strip()
                if content:
                    values.add(content)
                values.update(re.findall(r'(?im)(?:password|Password|Jwt__Key)\s*[=:]\s*([^;\r\n]+)', content))
    seed = folder / 'ops/seed.json'
    if seed.exists():
        for account in json.loads(seed.read_text())['accounts'].values():
            values.add(account['password'])
    values.update(v for k, v in os.environ.items()
                  if re.search(r'password|token|secret|jwt|credential', k, re.I) and len(v) >= 8)

    def redact(text):
        # Whole PEM blocks first, including keys that were not generated here.
        text = re.sub(r'-----BEGIN ([A-Z0-9 ]+)-----.*?-----END \1-----',
                      '[REDACTED PEM]', text, flags=re.S)
        for value in sorted(values, key=len, reverse=True):
            text = text.replace(value, '[REDACTED]')
        text = re.sub(r'(?i)\bBearer\s+[^\s"\\]+', 'Bearer [REDACTED]', text)
        text = re.sub(r'\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+', '[REDACTED JWT]', text)
        text = re.sub(r'\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+)\b', '[REDACTED TOKEN]', text)
        text = re.sub(r'(?i)(\b(?:password|passwd|token|secret|jwt__key)\s*[=:]\s*)([^\s;]+)',
                      r'\1[REDACTED]', text)
        text = re.sub(r"(?i)(IDENTIFIED(?: WITH [a-z0-9_]+)? BY\s*)'[^']*'", r"\1'[REDACTED]'", text)
        return text
    return redact


def collect(folder, state):
    redact = sanitizer(folder)

    def emit(section, data):
        # JSON escapes newlines/control bytes, including Actions workflow commands.
        def safe(value):
            if isinstance(value, str):
                return redact(value)
            if isinstance(value, dict):
                return {k: safe(v) for k, v in value.items()}
            if isinstance(value, list):
                return [safe(v) for v in value]
            return value
        print(json.dumps({'section': section, 'data': safe(data)}, ensure_ascii=True))

    def command(section, args):
        try:
            result = subprocess.run(args, capture_output=True, text=True, errors='replace', timeout=15)
            emit(section, {'exitCode': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr})
            return result.stdout if result.returncode == 0 else ''
        except (OSError, subprocess.TimeoutExpired):
            # Exception/timeout payloads can contain unsanitized output.
            emit(section, {'unavailable': True})
            return ''

    emit('provisioning',provisioning_evidence(folder))
    command('docker-version', ['docker', 'version', '--format', '{{json .Server.Version}}'])
    command('compose-version', ['docker', 'compose', 'version', '--short'])
    failure = folder / 'ops/transport-failure.json'
    if failure.exists():
        try:
            if failure.stat().st_size <= 16384:
                emit('driver-transport-failure', json.loads(failure.read_text()))
        except (OSError, ValueError):
            emit('driver-transport-failure', {'unavailable': True})
    ids = command('project-containers', ['docker', 'ps', '-aq', '--filter',
                                        'label=com.docker.compose.project=' + state['project']]).splitlines()
    for cid in ids:
        if not re.fullmatch(r'[a-f0-9]{12,64}', cid):
            continue
        # Only selected lifecycle fields, never Config.Env or complete inspect.
        try:
            result = subprocess.run(['docker', 'inspect', cid], capture_output=True,
                                    text=True, errors='replace', timeout=15)
            obj = json.loads(result.stdout)[0]
            labels = obj['Config']['Labels']
            if labels.get('io.er.staging.id') != state['id']:
                emit('ownership', {'container': cid, 'rejected': True})
                continue
            service = labels.get('com.docker.compose.service')
            status = obj['State']
            emit('container-state', {'container': cid, 'service': service,
                                    'restartCount': obj.get('RestartCount'),
                                    'state': {k: status.get(k) for k in
                                              ('Status', 'Running', 'Restarting', 'OOMKilled', 'ExitCode',
                                               'Error', 'StartedAt', 'FinishedAt', 'Health')}})
        except (OSError, subprocess.TimeoutExpired, ValueError, KeyError, IndexError):
            emit('container-state', {'container': cid, 'unavailable': True})
            continue
        if service in ('db', 'volume-init', 'api'):
            command(service + '-logs', ['docker', 'logs', '--timestamps', '--tail', '200', cid])
        if service == 'db' and status.get('Running'):
            command('db-filesystem', ['docker', 'exec', cid, 'sh', '-c',
                    'id; stat -c "%n uid=%u gid=%g mode=%a" /var/lib/mysql /var/lib/mysql/mysql '
                    '/var/run/mysqld /var/run/mysqld/mysqld.sock /tmp /run/secrets/*; '
                    'for f in /run/secrets/*; do test -r "$f" || echo "unreadable: $f"; done'])
            command('db-health-replay', ['docker', 'exec', cid, 'sh', '/opt/er/db-health.sh'])
            # Socket access is the same local administrative transport as bootstrap.
            # No DDL, password arguments, user tables or account hashes in evidence.
            command('db-runtime', ['docker', 'exec', cid, 'mysql',
                    '--defaults-extra-file=/run/secrets/root.cnf', '--protocol=SOCKET',
                    '--connect-timeout=3', '--batch', '-e',
                    "SELECT VERSION(),@@hostname,@@datadir; "
                    "SHOW GLOBAL VARIABLES WHERE Variable_name IN "
                    "('tls_version','require_secure_transport','skip_networking',"
                    "'ssl_ca','ssl_cert','ssl_key','log_error'); "
                    "SELECT id FROM er_stage_control.identity;"])
