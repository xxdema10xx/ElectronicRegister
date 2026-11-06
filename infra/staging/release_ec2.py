"""EC2 staging release driven by infra/staging/release-ec2.sh, one phase per invocation.

Phases: check, backup, deploy, switch, seed-demo, verify, rollback (docs/operations/EC2_RELEASE.md).
The old version is only ever stopped (`docker compose stop`) and started again; its volume,
images, vhosts and certificate directory are never removed or modified. Nginx changes are
limited to the two sites-enabled links of the old namespace and the two of the new one.
Secrets are never printed: the old state is read only for namespace, port and paths.
"""
import datetime
import hashlib
import http.client
import ipaddress
import json
import os
from pathlib import Path
import re
import shutil
import socket
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.request

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
ENV = os.environ.get
BASE = Path(ENV('ER_RELEASE_BASE', '/srv/electronicregister-staging'))
NGINX = Path(ENV('ER_RELEASE_NGINX', '/etc/nginx'))
LOGROTATE = Path(ENV('ER_RELEASE_LOGROTATE', '/etc/logrotate.d'))
NGINX_LOGS = Path(ENV('ER_RELEASE_NGINX_LOGS', '/var/log/er-stage'))
ACME = Path(ENV('ER_RELEASE_ACME', '/var/lib/er-stage-acme'))
DOMAIN = ENV('ER_RELEASE_DOMAIN', 'staging.consoliprojects.dev')
OLD_PROJECT = ENV('ER_RELEASE_OLD_PROJECT', 'er-stage-14d2fd709a09')
OLD_PORT = int(ENV('ER_RELEASE_OLD_PORT', '18080'))
OLD_STATE = Path(ENV('ER_RELEASE_OLD_STATE', '/srv/electronicregister-staging/state-8e7d9ecd42c001f1fa9b30e120535ce1bb0ef6e8-20261031T001359Z'))
REPOSITORY = 'xxdema10xx/ElectronicRegister'
WORKFLOWS = {
    '.github/workflows/mysql84-remediation-candidate.yml': 'MySQL 8.4 corrective release and upgrade',
    '.github/workflows/integration-full.yml': 'Integration full',
    '.github/workflows/staging-current.yml': 'Staging package on current code',
    '.github/workflows/staging-package.yml': 'Historical er-quality-1 package',
    '.github/workflows/verify.yml': 'Verify portable backend',
}
MIN_CERT_DAYS = 14
FIRST_PORT, LAST_PORT = 18081, 18180
SUBNETS = [f'172.30.{n}.0/24' for n in range(253, 256)] + [f'172.31.{n}.0/24' for n in range(240, 256)]
MIN_DISK_GIB = 8
MIN_MEM_BEFORE_MIB = 2048     # API build + new MySQL while the old version keeps running
MIN_MEM_AFTER_MIB = 768
HOST_STEPS = ['config', 'build', 'db', 'sql-check', 'provision', 'compatibility', 'release-preflight', 'tls', 'seed', 'preflight', 'start']
PY = sys.executable


class Fail(RuntimeError):
    pass


def require(value, message):
    if not value:
        raise Fail(message)


def say(text=''):
    print(text, flush=True)


def now_stamp():
    return datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')


def run(cmd, check=True, capture=True, timeout=None, cwd=None, input=None):
    p = subprocess.run([str(c) for c in cmd], capture_output=capture, text=True, timeout=timeout, cwd=cwd, input=input)
    if check and p.returncode:
        raise Fail(f'{Path(str(cmd[0])).name} {" ".join(str(c) for c in cmd[1:3])} uscito con codice {p.returncode}')
    return p


def out(cmd, **kw):
    return run(cmd, **kw).stdout.strip()


def stream(cmd, cwd=None):
    """Run a long tool with its own (secret-free) output streamed to the log."""
    say('$ ' + ' '.join(str(c) for c in cmd))
    p = subprocess.run([str(c) for c in cmd], cwd=cwd)
    require(p.returncode == 0, f'{" ".join(str(c) for c in cmd[1:4])} fallito (codice {p.returncode})')


def git(repo, *args):
    return out(['git', '-c', 'safe.directory=*', '-C', repo, *args])


def revision():
    rev = git(ROOT, 'rev-parse', 'HEAD')
    require(re.fullmatch(r'[a-f0-9]{40}', rev), 'Revisione non valida')
    return rev


REV = None
REL = None


def release_dir():
    return BASE / f'release-{REV}'


# ---------------------------------------------------------------------------- reporting

class Report:
    def __init__(self, phase):
        self.phase, self.failed, self.warnings = phase, [], []

    def ok(self, name, detail=''):
        say(f'OK    {name}' + (f' - {detail}' if detail else ''))

    def warn(self, name, detail=''):
        self.warnings.append(name)
        say(f'WARN  {name}' + (f' - {detail}' if detail else ''))

    def fail(self, name, detail=''):
        self.failed.append(name)
        say(f'FAIL  {name}' + (f' - {detail}' if detail else ''))

    def check(self, name, condition, detail='', fatal=False):
        (self.ok if condition else self.fail)(name, detail)
        if fatal and not condition:
            raise Fail(name)
        return condition

    def guard(self, name, function):
        try:
            return function()
        except (Fail, OSError, ValueError, KeyError, subprocess.SubprocessError, json.JSONDecodeError) as error:
            self.fail(name, f'{type(error).__name__}: {error}')
            return None


# ---------------------------------------------------------------------------- host facts

def version_tuple(text):
    match = re.search(r'(\d+)\.(\d+)', text)
    return tuple(map(int, match.groups())) if match else (0, 0)


def mem_available_mib():
    for line in Path('/proc/meminfo').read_text().splitlines():
        if line.startswith('MemAvailable:'):
            return int(line.split()[1]) // 1024
    raise Fail('MemAvailable non disponibile')


def disk_free_gib(path):
    return shutil.disk_usage(path).free / 2**30


def docker_root():
    return Path(out(['docker', 'info', '--format', '{{.DockerRootDir}}']) or '/var/lib/docker')


def containers(project):
    ids = out(['docker', 'ps', '-aq', '--filter', 'label=com.docker.compose.project=' + project]).split()
    result = {}
    for cid in ids:
        obj = json.loads(out(['docker', 'inspect', cid]))[0]
        service = obj['Config']['Labels'].get('com.docker.compose.service', '?')
        result.setdefault(service, []).append(obj)
    return result


def service_state(project, service):
    objs = containers(project).get(service, [])
    if not objs:
        return 'missing'
    state = objs[0]['State']
    if not state.get('Running'):
        return 'stopped'
    return state.get('Health', {}).get('Status', 'running')


def loopback_ready(port):
    connection = http.client.HTTPConnection('127.0.0.1', port, timeout=6)
    try:
        connection.request('GET', '/health/ready', headers={'Host': DOMAIN, 'X-Forwarded-Proto': 'https'})
        response = connection.getresponse()
        response.read()
        return response.status
    except OSError:
        return None
    finally:
        connection.close()


def public_ready():
    """GET https://DOMAIN/health/ready with the system trust store; returns (status, body, leaf sha256)."""
    context = ssl.create_default_context()
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    connection = http.client.HTTPSConnection(DOMAIN, 443, context=context, timeout=10)
    try:
        connection.request('GET', '/health/ready', headers={'User-Agent': 'er-release'})
        response = connection.getresponse()
        leaf = hashlib.sha256(connection.sock.getpeercert(binary_form=True)).hexdigest()
        body = response.read().decode('utf-8', 'replace')[:200]
        return response.status, body, leaf
    except (OSError, ssl.SSLError, http.client.HTTPException) as error:
        return None, type(error).__name__, None
    finally:
        connection.close()


def wait_public(expect_log=None, timeout=60):
    """Poll public HTTPS readiness; when expect_log is given, also require that this request
    reached the vhost writing that access log (proves which namespace served it)."""
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        before = expect_log.stat().st_size if expect_log and expect_log.exists() else 0
        last = public_ready()
        if last[0] == 200:
            if expect_log is None:
                return True, last
            time.sleep(1)
            if expect_log.exists() and expect_log.stat().st_size > before:
                return True, last
        time.sleep(3)
    return False, last


def port_free(port):
    for family, address in ((socket.AF_INET, '127.0.0.1'), (socket.AF_INET, '0.0.0.0')):
        with socket.socket(family) as probe:
            try:
                probe.bind((address, port))
            except OSError:
                return False
    return True


def docker_published_ports():
    used = set()
    for line in out(['docker', 'ps', '-a', '--format', '{{.Ports}}']).splitlines():
        used.update(int(p) for p in re.findall(r':(\d+)->', line))
    for cid in out(['docker', 'ps', '-aq']).split():
        bindings = json.loads(out(['docker', 'inspect', '--format', '{{json .HostConfig.PortBindings}}', cid])) or {}
        for items in bindings.values():
            used.update(int(b['HostPort']) for b in items or [] if b.get('HostPort'))
    return used


def nginx_ports():
    text = run(['nginx', '-T'], check=False).stdout
    return {int(p) for p in re.findall(r'proxy_pass\s+https?://127\.0\.0\.1:(\d+)', text)}


def free_port(exclude=()):
    taken = docker_published_ports() | nginx_ports() | set(exclude)
    for port in range(FIRST_PORT, LAST_PORT + 1):
        if port not in taken and port_free(port):
            return port
    raise Fail(f'Nessuna porta libera fra {FIRST_PORT} e {LAST_PORT}')


def used_subnets():
    nets = []
    for name in out(['docker', 'network', 'ls', '-q']).split():
        for cfg in json.loads(out(['docker', 'network', 'inspect', name]))[0].get('IPAM', {}).get('Config') or []:
            if 'Subnet' in cfg and ':' not in cfg['Subnet']:
                nets.append(ipaddress.ip_network(cfg['Subnet']))
    for route in json.loads(out(['ip', '-json', 'route'])):
        if route.get('dst', 'default') != 'default' and ':' not in route['dst']:
            nets.append(ipaddress.ip_network(route['dst'], strict=False))
    return nets


def free_subnet():
    used = used_subnets()
    for candidate in SUBNETS:
        net = ipaddress.ip_network(candidate)
        if not any(net.overlaps(other) for other in used):
            return candidate
    raise Fail('Nessuna subnet /24 privata libera')


# ---------------------------------------------------------------------------- certificate

def openssl(*args, input=None):
    return run(['openssl', *args], input=input).stdout


def cert_facts(fullchain, key=None):
    text = openssl('x509', '-in', fullchain, '-noout', '-subject', '-enddate', '-ext', 'subjectAltName', '-fingerprint', '-sha256')
    names = re.findall(r'DNS:([^,\s]+)', text)
    end = re.search(r'notAfter=(.+)', text).group(1).strip()
    expires = datetime.datetime.strptime(end, '%b %d %H:%M:%S %Y %Z').replace(tzinfo=datetime.timezone.utc)
    fingerprint = re.search(r'Fingerprint=([0-9A-F:]+)', text).group(1).replace(':', '').lower()
    facts = dict(san=names, expires=expires, fingerprint=fingerprint,
                 days=(expires - datetime.datetime.now(datetime.timezone.utc)).total_seconds() / 86400)
    if key:
        # Public key of the certificate and of the private key, both normalised to PEM by openssl.
        cert_pub = run(['openssl', 'x509', '-in', fullchain, '-noout', '-pubkey']).stdout
        cert_pem = run(['openssl', 'pkey', '-pubin'], input=cert_pub).stdout.strip()
        key_pem = run(['openssl', 'pkey', '-in', key, '-pubout']).stdout.strip()
        facts['keyMatches'] = bool(cert_pem) and cert_pem == key_pem
    return facts


def cert_layout(project):
    """Versioned layout: <dir>/versions/<name>/{fullchain,privkey}.pem, active -> versions/<name>,
    fullchain.pem -> active/fullchain.pem, privkey.pem -> active/privkey.pem."""
    folder = NGINX / project
    active = folder / 'active'
    require(active.is_symlink(), f'{active} non e un link')
    target = os.readlink(active)
    require(re.fullmatch(r'versions/[A-Za-z0-9._-]+', target), f'active punta a {target}, atteso versions/<nome>')
    version = folder / target
    links = {}
    for name in ('fullchain.pem', 'privkey.pem'):
        link = folder / name
        require(link.is_symlink() and os.readlink(link) in (f'active/{name}', str(active / name)), f'{link} non punta ad active/{name}')
        links[name] = os.readlink(link)
        require((version / name).is_file(), f'{version / name} mancante')
    return dict(folder=folder, active=active, target=target, version=version, links=links)


def sha256_file(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def copy_like(source, dest, reference):
    shutil.copyfile(source, dest)
    st = os.stat(reference)
    os.chown(dest, st.st_uid, st.st_gid)
    os.chmod(dest, st.st_mode & 0o7777)
    require(sha256_file(source) == sha256_file(dest), f'SHA-256 diverso dopo la copia di {Path(source).name}')


def mkdir_like(path, reference):
    st = os.stat(reference)
    path.mkdir(mode=0o700)
    os.chown(path, st.st_uid, st.st_gid)
    os.chmod(path, st.st_mode & 0o7777)


def install_certificate(project, report):
    """Copy the active certificate of the old namespace into a new versioned directory with
    the same layout and permissions. The old directory is only read."""
    old = cert_layout(OLD_PROJECT)
    new = NGINX / project
    if new.exists():
        layout = cert_layout(project)
        for name in ('fullchain.pem', 'privkey.pem'):
            require(sha256_file(layout['version'] / name) == sha256_file(old['version'] / name), f'{new}: {name} diverso dal certificato attivo')
        report.ok('certificato del nuovo namespace gia installato e identico', str(new))
        return
    mkdir_like(new, old['folder'])
    mkdir_like(new / 'versions', old['folder'] / 'versions')
    version = new / old['target']
    mkdir_like(version, old['version'])
    for name in ('fullchain.pem', 'privkey.pem'):
        copy_like(old['version'] / name, version / name, old['version'] / name)
    os.symlink(old['target'], new / 'active')
    for name, target in old['links'].items():
        os.symlink(target if not target.startswith('/') else str(new / 'active' / name), new / name)
    layout = cert_layout(project)
    for name in ('fullchain.pem', 'privkey.pem'):
        a, b = os.stat(old['version'] / name), os.stat(layout['version'] / name)
        require((a.st_mode, a.st_uid, a.st_gid) == (b.st_mode, b.st_uid, b.st_gid), f'permessi diversi per {name}')
    report.ok('certificato copiato nel nuovo namespace con verifica SHA-256 e stessi permessi', f'{new}/{old["target"]}')


# ---------------------------------------------------------------------------- nginx

def enabled():
    return NGINX / 'sites-enabled'


def available():
    return NGINX / 'sites-available'


def vhost_names(project):
    return [f'{project}-http.conf', f'{project}-https.conf']


def link_target_style(name):
    """Mirror how the old links point to sites-available (absolute or relative)."""
    path = enabled() / name
    if path.is_symlink():
        target = os.readlink(path)
        return 'relative' if not target.startswith('/') else 'absolute'
    return 'absolute'


def make_link(name, style):
    target = (f'../sites-available/{name}' if style == 'relative' else str(available() / name))
    os.symlink(target, enabled() / name)


def is_our_link(name):
    path = enabled() / name
    if not path.is_symlink():
        return False
    return (enabled() / os.readlink(path)).resolve() == (available() / name).resolve()


def nginx_test():
    p = run(['nginx', '-t'], check=False)
    return p.returncode == 0, (p.stderr or p.stdout).strip().splitlines()[-3:]


def nginx_reload():
    run(['systemctl', 'reload', 'nginx'])
    time.sleep(2)


def link_state(project):
    return {name: is_our_link(name) for name in vhost_names(project)}


def set_links(enable, disable, style):
    """Disable the links of one namespace and enable those of the other. Only symlinks that
    point to the matching sites-available file are touched; vhost files are never removed."""
    for name in disable:
        path = enabled() / name
        if path.is_symlink():
            require(is_our_link(name), f'{path} non punta a sites-available/{name}: non lo tocco')
            path.unlink()
        else:
            require(not path.exists(), f'{path} esiste e non e un link: non lo tocco')
    for name in enable:
        require((available() / name).is_file(), f'{available() / name} mancante')
        path = enabled() / name
        if not path.is_symlink():
            require(not path.exists(), f'{path} esiste e non e un link: non lo tocco')
            make_link(name, style)


# ---------------------------------------------------------------------------- old / new versions

def old_state():
    state = json.loads((OLD_STATE / 'state.json').read_text())
    require(state['project'] == OLD_PROJECT, f"namespace della vecchia versione {state['project']}, atteso {OLD_PROJECT}")
    require(int(state['port']) == OLD_PORT, f"porta della vecchia versione {state['port']}, attesa {OLD_PORT}")
    source = Path(state['repository'])
    require((source / 'infra/staging/compose.yaml').is_file(), f'compose della vecchia versione assente in {source}')
    require((OLD_STATE / '.env').is_file(), 'file .env della vecchia versione assente')
    return dict(project=state['project'], port=int(state['port']), source=source, revision=state['revision'],
                subnet=state['subnet'], id=state['id'])


def compose(state_dir, source, *args, check=True):
    """docker compose from the version's own folder with its own env file (as host.py does)."""
    return run(['docker', 'compose', '--env-file', Path(state_dir) / '.env', '-f', Path(source) / 'infra/staging/compose.yaml', *args],
               cwd=source, check=check)


def wait_healthy(project, services=('db', 'api'), timeout=300):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        states = {s: service_state(project, s) for s in services}
        if all(v == 'healthy' for v in states.values()):
            return states
        time.sleep(5)
    return {s: service_state(project, s) for s in services}


def deploy_record():
    path = release_dir() / 'deploy.json'
    return json.loads(path.read_text()) if path.exists() else None


def save_record(record):
    path = release_dir() / 'deploy.json'
    tmp = path.with_suffix('.tmp')
    tmp.write_text(json.dumps(record, indent=2) + '\n')
    os.chmod(tmp, 0o600)
    os.replace(tmp, path)


def new_version():
    record = deploy_record()
    require(record and record.get('project'), 'Deploy non registrato: eseguire prima la fase deploy')
    return record


def host_py(record, *args, cwd=None):
    stream([PY, '-B', Path(record['source']) / 'infra/staging/host.py', '--state', record['state'], *args], cwd=record['source'])


def memory_report(report, label):
    available_mib = mem_available_mib()
    rows = out(['docker', 'stats', '--no-stream', '--format', '{{.Name}}\t{{.MemUsage}}']).splitlines()
    total = 0.0
    for row in rows:
        name, usage = row.split('\t')
        used = usage.split('/')[0].strip()
        match = re.match(r'([\d.]+)\s*([KMG]i?B)', used)
        if match:
            value = float(match.group(1)) * {'KiB': 1 / 1024, 'KB': 1 / 1024, 'MiB': 1, 'MB': 1, 'GiB': 1024, 'GB': 1024}.get(match.group(2), 0)
            total += value
            say(f'      {name}: {value:.0f} MiB')
    report.ok(f'memoria {label}', f'container {total:.0f} MiB, MemAvailable {available_mib} MiB')
    return total, available_mib


# ---------------------------------------------------------------------------- phases

def phase_check():
    r = Report('check')
    say(f'Release {REV} -> {DOMAIN}; vecchia versione {OLD_PROJECT} (porta {OLD_PORT})')
    r.check('architettura x86_64', out(['uname', '-m']) == 'x86_64')
    docker = out(['docker', 'version', '--format', '{{.Server.Version}}'])
    r.check('Docker Engine >= 28', version_tuple(docker) >= (28, 0), docker)
    compose_version = out(['docker', 'compose', 'version', '--short'])
    r.check('Docker Compose >= 2.24', version_tuple(compose_version) >= (2, 24), compose_version)
    r.ok('Python', sys.version.split()[0])
    for tool in ('git', 'openssl', 'nginx', 'systemctl', 'ip'):
        r.check(f'{tool} disponibile', shutil.which(tool) is not None)
    free = min(disk_free_gib(BASE), disk_free_gib(docker_root()))
    r.check(f'spazio libero >= {MIN_DISK_GIB} GiB', free >= MIN_DISK_GIB, f'{free:.1f} GiB')
    mem = mem_available_mib()
    r.check(f'RAM disponibile >= {MIN_MEM_BEFORE_MIB} MiB (vecchia accesa + nuova in build)', mem >= MIN_MEM_BEFORE_MIB, f'{mem} MiB')
    r.check('commit: checkout pulito', not git(ROOT, 'status', '--porcelain', '--untracked-files=normal'), REV)
    old = r.guard('stato della vecchia versione leggibile', old_state)
    if old:
        r.check('commit diverso da quello della vecchia versione', old['revision'] != REV, old['revision'])
        for service in ('db', 'api'):
            state = service_state(old['project'], service)
            r.check(f'vecchia versione: {service} healthy', state == 'healthy', state)
        r.check('vecchia versione: /health/ready su 127.0.0.1', loopback_ready(old['port']) == 200, f"porta {old['port']}")
        for kind, name in (('volume', f"{old['project']}_db-data"), ('network', f"{old['project']}_backend")):
            r.check(f'vecchia versione: {kind} {name}', run(['docker', kind, 'inspect', name], check=False).returncode == 0)
    ok, lines = nginx_test()
    r.check('nginx -t', ok, ' | '.join(lines))
    state = link_state(OLD_PROJECT)
    r.check('vhost della vecchia versione attive in sites-enabled', all(state.values()), json.dumps(state))
    record = deploy_record()
    if record and record.get('project') and any(link_state(record['project']).values()):
        r.warn('le vhost del nuovo namespace risultano gia attive (switch eseguito?)', record['project'])
    status, body, leaf = public_ready()
    r.check(f'HTTPS pubblico https://{DOMAIN}/health/ready', status == 200 and '"ready"' in body, f'HTTP {status} {body[:60]}')
    layout = r.guard('certificato: layout a versioni del vecchio namespace', lambda: cert_layout(OLD_PROJECT))
    if layout:
        r.ok('certificato attivo', f"{layout['folder']}/active -> {layout['target']}")
        facts = r.guard('certificato leggibile', lambda: cert_facts(layout['version'] / 'fullchain.pem', layout['version'] / 'privkey.pem'))
        if facts:
            r.check('certificato: SAN contiene il dominio', DOMAIN in facts['san'], ','.join(facts['san']))
            r.check(f'certificato: scadenza fra almeno {MIN_CERT_DAYS} giorni', facts['days'] >= MIN_CERT_DAYS,
                    f"{facts['expires']:%Y-%m-%dT%H:%M:%SZ} ({facts['days']:.0f} giorni)")
            r.check('certificato: chiave privata corrispondente', facts['keyMatches'])
            r.check('certificato: e quello servito pubblicamente da nginx', leaf == facts['fingerprint'], 'impronta SHA-256 ' + facts['fingerprint'][:16])
            name = layout['target'].split('/', 1)[1]
            known = {'sha256(fullchain.pem)': sha256_file(layout['version'] / 'fullchain.pem'), 'sha256 del certificato (DER)': facts['fingerprint']}
            match = [k for k, v in known.items() if v == name.lower()]
            say(f"      nome della versione: {match[0] if match else 'non e lo sha256 di fullchain.pem ne del certificato: lo riuso identico'}")
    if not record:
        sources = [p for p in BASE.glob(f'source-{REV}')] + [p for p in BASE.glob(f'state-{REV}-*')]
        r.check('cartelle del nuovo commit non ancora occupate', not sources, ', '.join(map(str, sources)))
        try:
            r.ok('porta loopback che verra usata', str(free_port()))
            r.ok('subnet che verra usata', free_subnet())
        except Fail as error:
            r.fail('risorse libere per la nuova versione', str(error))
    else:
        r.ok('deploy gia registrato per questo commit', f"{record.get('project')} porta {record.get('port')}")
    ci(r)
    return r


def ci(r_real):
    r = r_real
    url = f'https://api.github.com/repos/{REPOSITORY}/actions/runs?head_sha={REV}&per_page=100'
    request = urllib.request.Request(url, headers={'User-Agent': 'er-release-check', 'Accept': 'application/vnd.github+json'})
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            runs = json.loads(response.read())['workflow_runs']
    except (OSError, urllib.error.URLError, ValueError, KeyError) as error:
        r.warn('CI su GitHub non raggiungibile: verificarla a mano', type(error).__name__)
        return
    # Inside GitHub Actions the run under way is the one exercising this script: report only.
    report_only = ENV('ER_RELEASE_CI_CHECK') == 'report'
    if report_only:
        require(ENV('GITHUB_ACTIONS') == 'true', 'ER_RELEASE_CI_CHECK=report e consentito solo in GitHub Actions')
        r = type('ReportOnly', (), dict(check=lambda self, name, ok, detail='': r_real.ok(name, detail) if ok else r_real.warn(name, detail),
                                         fail=lambda self, name, detail='': r_real.warn(name, detail)))()
    for path, label in WORKFLOWS.items():
        mine = sorted((x for x in runs if x.get('path') == path and x.get('head_sha') == REV), key=lambda x: (x['created_at'], x.get('run_attempt', 1)))
        if not mine:
            r.fail(f'CI {label}: nessuna run per questo commit')
            continue
        last = mine[-1]
        r.check(f'CI {label}', last['status'] == 'completed' and last['conclusion'] == 'success',
                f"{last['status']}/{last['conclusion']} {last['html_url']}")


def redact_env(values):
    result = []
    for item in values or []:
        key, _, value = item.partition('=')
        if re.search(r'PASS|SECRET|TOKEN|KEY|CONNECTION|CREDENTIAL', key, re.I) and not key.endswith('_FILE'):
            value = '<omesso>'
        result.append(f'{key}={value}')
    return result


def phase_backup():
    r = Report('backup')
    old = old_state()
    dest = BASE / 'backups' / f'{now_stamp()}-pre-{REV[:12]}'
    (BASE / 'backups').mkdir(mode=0o700, exist_ok=True)
    dest.mkdir(mode=0o700)
    def save(name, text):
        path = dest / name
        path.write_text(text)
        os.chmod(path, 0o600)
    run(['tar', '-C', NGINX, '-czf', dest / 'nginx-sites.tar.gz', 'sites-available', 'sites-enabled'])
    os.chmod(dest / 'nginx-sites.tar.gz', 0o600)
    listing = out(['tar', '-tzvf', dest / 'nginx-sites.tar.gz'])
    save('nginx-sites.list.txt', listing + '\n')
    test = run(['nginx', '-T'], check=False)
    save('nginx-T.txt', test.stdout + test.stderr)
    r.check('configurazione nginx salvata (sites-available, sites-enabled, nginx -T)', test.returncode == 0, f'{len(listing.splitlines())} voci')
    save('docker-ps.txt', out(['docker', 'ps', '-a', '--filter', 'label=com.docker.compose.project=' + old['project'],
                                '--format', 'table {{.ID}}\t{{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}']) + '\n')
    inspected = []
    for objs in containers(old['project']).values():
        for obj in objs:
            obj['Config']['Env'] = redact_env(obj['Config'].get('Env'))
            inspected.append(obj)
    save('docker-inspect.json', json.dumps(inspected, indent=2) + '\n')
    r.check('docker ps/inspect della vecchia versione (variabili sensibili omesse)', len(inspected) >= 2, f'{len(inspected)} container')
    save('docker-volumes.txt', out(['docker', 'volume', 'ls']) + '\n')
    save('docker-networks.txt', out(['docker', 'network', 'ls']) + '\n')
    save('docker-images.txt', out(['docker', 'image', 'ls', '--digests']) + '\n')
    save('old-volume-network.json', out(['docker', 'volume', 'inspect', f"{old['project']}_db-data"]) + '\n' +
         out(['docker', 'network', 'inspect', f"{old['project']}_backend"]) + '\n')
    r.ok('volumi, reti e immagini elencati')
    links = {p.name: os.readlink(p) for p in enabled().iterdir() if p.is_symlink()}
    layout = cert_layout(OLD_PROJECT)
    cert = dict(links=links, certificate=dict(folder=str(layout['folder']), active=layout['target'],
                                              sha256={n: sha256_file(layout['version'] / n) for n in ('fullchain.pem', 'privkey.pem')}),
                oldVersion=dict(project=old['project'], port=old['port'], revision=old['revision'], source=str(old['source']), state=str(OLD_STATE)))
    save('release-context.json', json.dumps(cert, indent=2) + '\n')
    r.ok('link sites-enabled e impronte SHA-256 del certificato (nessuna chiave copiata)')
    sums = ''.join(f'{sha256_file(p)}  {p.name}\n' for p in sorted(dest.iterdir()) if p.is_file())
    save('SHA256SUMS', sums)
    r.ok('backup completato', str(dest))
    return r


def phase_deploy():
    r = Report('deploy')
    old = old_state()
    record = deploy_record()
    if record is None:
        source = BASE / f'source-{REV}'
        require(not source.exists(), f'{source} esiste gia: non lo riuso')
        existing = list(BASE.glob(f'state-{REV}-*'))
        require(not existing, 'stato gia presente per questo commit: ' + ', '.join(map(str, existing)))
        record = dict(revision=REV, source=str(source), state=str(BASE / f'state-{REV}-{now_stamp()}'),
                      port=free_port(exclude=[old['port']]), subnet=free_subnet(), steps=[], abandoned=[])
        require(port_free(record['port']), f"porta {record['port']} occupata")
        save_record(record)
        r.ok('nuova versione pianificata', f"porta 127.0.0.1:{record['port']}, subnet {record['subnet']}")
    else:
        require(record['revision'] == REV, 'deploy.json di un altro commit')
        r.ok('ripresa del deploy registrato', record['state'])
    mem = mem_available_mib()
    if 'start' not in record['steps']:
        require(mem >= MIN_MEM_BEFORE_MIB, f'RAM disponibile {mem} MiB < {MIN_MEM_BEFORE_MIB} MiB')
    require(min(disk_free_gib(BASE), disk_free_gib(docker_root())) >= MIN_DISK_GIB, 'spazio disco insufficiente')
    source = Path(record['source'])
    if not source.exists():
        run(['git', '-c', 'safe.directory=*', 'clone', '--no-hardlinks', '--quiet', '--no-checkout', ROOT, source])
        run(['git', '-c', 'safe.directory=*', '-c', 'advice.detachedHead=false', '-C', source, 'checkout', '--quiet', '--detach', REV])
    require(git(source, 'rev-parse', 'HEAD') == REV, f'{source} non e al commit {REV}')
    require(not git(source, 'status', '--porcelain', '--untracked-files=normal'), f'{source} non e pulito')
    r.ok('checkout del commit esatto', f'{source} @ {REV}')
    state = Path(record['state'])
    if not (state / 'state.json').exists():
        if state.exists():
            # A prepare interrupted before writing state.json: keep it, plan a fresh folder.
            record['abandoned'].append(str(state))
            state = BASE / f'state-{REV}-{now_stamp()}'
            record['state'] = str(state)
            save_record(record)
            r.warn('stato parziale conservato e sostituito da uno nuovo', record['abandoned'][-1])
        require(port_free(record['port']) and record['port'] not in docker_published_ports() | nginx_ports(), f"porta {record['port']} occupata")
        host_py(record, 'prepare', '--domain', DOMAIN, '--port', str(record['port']), '--subnet', record['subnet'], '--enable-import')
    new_state = json.loads((state / 'state.json').read_text())
    require(new_state['revision'] == REV and new_state['project'] != old['project'], 'stato nuovo incoerente')
    record['project'] = new_state['project']
    save_record(record)
    runtime = json.loads((state / 'public/runtime.json').read_text())
    r.ok('configurazione', json.dumps(dict(RolloutApproved=runtime['Deployment']['RolloutApproved'], ExpectedVersion=runtime['Readiness']['ExpectedVersion'],
                                           KnownProxies=runtime['Transport']['KnownProxies'], AllowedHosts=runtime['AllowedHosts'],
                                           InstitutionImport=runtime['Multitenancy'].get('InstitutionImport', {}).get('Enabled', False))))
    install_certificate(record['project'], r)
    for base_dir, label in ((NGINX_LOGS, 'log nginx'), (ACME, 'acme')):
        reference, target = base_dir / OLD_PROJECT, base_dir / record['project']
        if not target.exists():
            if reference.exists():
                mkdir_like(target, reference)
            else:
                target.mkdir(parents=True, mode=0o755)
        r.ok(f'cartella {label} del nuovo namespace', str(target))
    rotate = LOGROTATE / record['project']
    if (LOGROTATE / OLD_PROJECT).exists() and not rotate.exists():
        shutil.copyfile(state / 'nginx-logrotate.conf', rotate)
        os.chmod(rotate, 0o644)
        r.ok('logrotate del nuovo namespace', str(rotate))
    for step in HOST_STEPS:
        if step in record['steps']:
            say(f'      host.py {step}: gia completato')
            continue
        host_py(record, step)
        record['steps'].append(step)
        save_record(record)
    host_py(record, 'verify')
    r.check('nuova versione: verify', True)
    r.check('vecchia versione ancora healthy', service_state(old['project'], 'api') == 'healthy' and loopback_ready(old['port']) == 200, fatal=True)
    status, body, _ = public_ready()
    r.check('HTTPS pubblico invariato durante il deploy', status == 200, f'HTTP {status}')
    _, available_after = memory_report(r, 'con vecchia e nuova versione accese')
    r.check(f'RAM disponibile dopo il deploy >= {MIN_MEM_AFTER_MIB} MiB', available_after >= MIN_MEM_AFTER_MIB, f'{available_after} MiB')
    return r


def phase_switch():
    r = Report('switch')
    old = old_state()
    record = new_version()
    require(all(step in record['steps'] for step in HOST_STEPS), 'deploy non completato')
    project, state = record['project'], Path(record['state'])
    require(project != OLD_PROJECT, 'namespace nuovo uguale al vecchio')
    old_names, new_names = vhost_names(OLD_PROJECT), vhost_names(project)
    old_links, new_links = link_state(OLD_PROJECT), link_state(project)
    access_log = NGINX_LOGS / project / 'access.log'
    if all(new_links.values()) and not any(old_links.values()):
        r.ok('vhost gia commutate sul nuovo namespace')
    else:
        if any(service_state(project, s) != 'healthy' for s in ('db', 'api')):
            # e.g. after a rollback: same frozen images and data, no bootstrap or seed replay.
            host_py(record, 'restart')
        host_py(record, 'verify')
        layout = cert_layout(project)
        facts = cert_facts(layout['version'] / 'fullchain.pem', layout['version'] / 'privkey.pem')
        require(DOMAIN in facts['san'] and facts['days'] >= MIN_CERT_DAYS and facts['keyMatches'], 'certificato del nuovo namespace non valido')
        r.ok('nuova versione verificata e certificato valido')
        for kind, name in (('http', new_names[0]), ('https', new_names[1])):
            generated, installed = state / f'nginx-{kind}.conf', available() / name
            if installed.exists():
                require(installed.read_bytes() == generated.read_bytes(), f'{installed} diverso dalla vhost generata da host.py')
            else:
                tmp = available() / f'.{name}.tmp'
                shutil.copyfile(generated, tmp)
                os.chmod(tmp, 0o644)
                os.replace(tmp, installed)
        r.ok('vhost generate da host.py in sites-available', ', '.join(new_names))
        style = link_target_style(old_names[0])
        (release_dir() / 'link-style').write_text(style + '\n')
        ok, lines = nginx_test()
        require(ok, 'nginx -t fallisce gia prima dello scambio: nessuna modifica')
        set_links(new_names, old_names, style)
        r.ok('link scambiati', f'attive {", ".join(new_names)}')
        ok, lines = nginx_test()
        if not ok:
            set_links(old_names, new_names, style)
            restored, _ = nginx_test()
            r.fail('nginx -t fallito con le nuove vhost: link vecchi ripristinati, nessun reload', ' | '.join(lines))
            r.check('nginx -t dopo il ripristino', restored)
            return r
        nginx_reload()
        served, last = wait_public(access_log)
        if not served:
            set_links(old_names, new_names, style)
            restored, _ = nginx_test()
            if restored:
                nginx_reload()
            back, _ = wait_public(NGINX_LOGS / OLD_PROJECT / 'access.log')
            r.fail('HTTPS pubblico non servito dalla nuova versione: link vecchi ripristinati', f'HTTP {last[0] if last else None}')
            r.check('HTTPS pubblico di nuovo servito dalla vecchia versione', back)
            return r
        r.ok('HTTPS pubblico servito dalla nuova versione', f'{DOMAIN} -> 127.0.0.1:{record["port"]} (access log {access_log})')
    if service_state(old['project'], 'api') != 'stopped' or service_state(old['project'], 'db') != 'stopped':
        compose(OLD_STATE, old['source'], 'stop')
    states = {s: service_state(old['project'], s) for s in ('api', 'db')}
    r.check('vecchia versione fermata con docker compose stop (container, volume e immagini conservati)', all(v == 'stopped' for v in states.values()), json.dumps(states))
    served, last = wait_public(access_log)
    r.check('HTTPS pubblico ancora servito dalla nuova versione', served, f'HTTP {last[0] if last else None}')
    return r


def discover_new_projects():
    """New namespaces of this commit, from deploy.json or, after an interrupted deploy, from state files."""
    projects = {}
    record = deploy_record()
    if record and record.get('project'):
        projects[record['project']] = (Path(record['state']), Path(record['source']))
    for path in BASE.glob(f'state-{REV}-*/state.json'):
        state = json.loads(path.read_text())
        if state['project'] != OLD_PROJECT:
            projects.setdefault(state['project'], (path.parent, Path(state['repository'])))
    return projects


def phase_rollback():
    r = Report('rollback')
    old = old_state()
    if any(service_state(old['project'], s) != 'healthy' for s in ('db', 'api')):
        compose(OLD_STATE, old['source'], 'start')
    states = wait_healthy(old['project'])
    r.check('vecchia versione avviata con docker compose start', all(v == 'healthy' for v in states.values()), json.dumps(states), fatal=True)
    r.check('vecchia versione: /health/ready su 127.0.0.1', loopback_ready(old['port']) == 200, fatal=True)
    style_file = release_dir() / 'link-style'
    style = style_file.read_text().strip() if style_file.exists() else 'absolute'
    projects = discover_new_projects()
    remove = [name for project in projects for name in vhost_names(project)]
    set_links(vhost_names(OLD_PROJECT), remove, style)
    r.ok('link della vecchia versione attivi, link nuovi tolti', ', '.join(vhost_names(OLD_PROJECT)))
    ok, lines = nginx_test()
    r.check('nginx -t', ok, ' | '.join(lines), fatal=True)
    nginx_reload()
    served, last = wait_public(NGINX_LOGS / OLD_PROJECT / 'access.log')
    r.check('HTTPS pubblico servito dalla vecchia versione', served, f'HTTP {last[0] if last else None}')
    for project, (state, source) in projects.items():
        if (state / '.env').exists() and containers(project):
            compose(state, source, 'stop')
        stopped = {s: service_state(project, s) for s in ('api', 'db')}
        r.check(f'nuova versione {project} fermata (dati conservati)', all(v in ('stopped', 'missing') for v in stopped.values()), json.dumps(stopped))
    return r


def require_switched(r):
    record = new_version()
    require(all(link_state(record['project']).values()) and not any(link_state(OLD_PROJECT).values()), 'vhost non commutate: eseguire prima switch')
    served, last = wait_public(NGINX_LOGS / record['project'] / 'access.log')
    r.check('HTTPS pubblico servito dalla nuova versione', served, f'HTTP {last[0] if last else None}', fatal=True)
    return record


def demo(record, command):
    stream([PY, '-B', Path(record['source']) / 'infra/staging/demo_seed.py', command, '--state', record['state'],
            '--out', release_dir() / 'demo', '--domain', DOMAIN], cwd=record['source'])


def phase_seed_demo():
    r = Report('seed-demo')
    record = require_switched(r)
    demo(record, 'seed')
    r.ok('dati demo creati tramite API HTTPS', f"credenziali in {release_dir() / 'demo/credenziali-demo.txt'} (0600)")
    return r


def phase_verify():
    r = Report('verify')
    record = require_switched(r)
    status, body, _ = public_ready()
    r.check(f'https://{DOMAIN}/health/ready', status == 200 and '"ready"' in body, f'HTTP {status} {body[:60]}', fatal=True)
    host_py(record, 'verify')
    ops = Path(record['state']) / 'ops'
    loopback = ENV('ER_RELEASE_SMOKE') == 'loopback'
    if loopback:
        require(ENV('GITHUB_ACTIONS') == 'true', 'ER_RELEASE_SMOKE=loopback e consentito solo in GitHub Actions')
    marker = ops / 'http-audit-started'
    if marker.exists():
        reports = sorted(ops.glob('http-smoke-*.json'))
        passed = [p for p in reports if json.loads(p.read_text()).get('success')]
        r.check('smoke gia eseguito su questo staging (una sola volta per namespace): esito registrato', bool(passed),
                f'{passed[-1].name if passed else "nessun report riuscito: serve un nuovo namespace"}')
    elif loopback:
        say('      CI: smoke in modalita loopback (host.py http), il container ops non raggiunge il dominio pubblico del runner')
        host_py(record, 'http')
        r.ok('smoke host.py http (loopback, solo CI)')
    else:
        host_py(record, 'smoke')
        r.ok('smoke HTTPS pubblico (host.py smoke)')
    demo(record, 'verify')
    r.ok('dati demo e isolamento fra i due istituti verificati')
    return r


PHASES = {'check': phase_check, 'backup': phase_backup, 'deploy': phase_deploy, 'switch': phase_switch,
          'seed-demo': phase_seed_demo, 'verify': phase_verify, 'rollback': phase_rollback}


def main():
    global REV
    require(len(sys.argv) == 2 and sys.argv[1] in PHASES, 'Uso: release-ec2.sh ' + '|'.join(PHASES))
    require(sys.platform.startswith('linux') and os.geteuid() == 0, 'Eseguire come root (sudo)')
    REV = revision()
    phase = sys.argv[1]
    say(f'== {phase} {now_stamp()} commit {REV} python {sys.version.split()[0]}')
    report = PHASES[phase]()
    if report.failed:
        raise Fail(f'{len(report.failed)} controlli falliti: ' + '; '.join(report.failed))


if __name__ == '__main__':
    try:
        main()
    except (Fail, OSError, ValueError, KeyError, subprocess.SubprocessError, json.JSONDecodeError) as error:
        print(f'ERRORE: {type(error).__name__}: {error}', flush=True)
        sys.exit(1)
