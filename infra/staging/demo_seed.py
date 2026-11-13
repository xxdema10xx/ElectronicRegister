"""Demo data for the EC2 staging, created through the public HTTPS API.

Only institutions and their first InstitutionAdmin come from tools/OwnerProvisioning (the
documented owner procedure, run as the non-root er_migrator over TLS VerifyFull). Everything
else uses the API: accounts, academic structure, subjects, assignments, the student import
(EmailMode=None, then POST /api/institution/accounts), calendar settings, exams, submissions
and the grades they produce. No SQL writes.

Idempotent: event ids are derived from the staging id, resources are looked up before being
created and every temporary password is stored (root-only, 0600) before it is used, so a
rerun after a crash continues instead of duplicating. A lost temporary password is replaced
with the owner reset procedure, never guessed.

Nothing secret is printed: only step names, HTTP status codes and counts.
"""
import argparse
import datetime
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
import secrets
import ssl
import subprocess
import sys
import time
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parent))
import host  # noqa: E402

require = host.require
WRITE_BUDGET = 45          # API: 60 writes/minute per client IP (Transport:SensitivePermitLimit)
TOKEN_MAX_AGE = 600        # access tokens live about 15 minutes; refresh well before
SUBMITTERS = 5             # of 6 students per class; the sixth leaves the exam open

FIRST = ['Alessia', 'Bruno', 'Chiara', 'Davide', 'Elena', 'Fabio', 'Giorgia', 'Luca', 'Marta', 'Nicola', 'Olivia', 'Paolo',
         'Rita', 'Sergio', 'Teresa', 'Ugo', 'Valeria', 'Walter', 'Ylenia', 'Zeno', 'Bianca', 'Carlo', 'Diana', 'Enrico']
LAST = ['Amato', 'Barbieri', 'Caruso', 'De Santis', 'Esposito', 'Fontana', 'Galli', 'Lombardi', 'Mancini', 'Neri', 'Orlando', 'Pellegrini',
        'Rinaldi', 'Sala', 'Testa', 'Valentini', 'Vitale', 'Zanetti', 'Bellini', 'Colombo', 'Donati', 'Ferrara', 'Gatti', 'Marino']

INSTITUTES = [
    dict(key='aurora', code='DEMO_AURORA', name='Istituto Dimostrativo Aurora (dati finti)', mail='aurora.example.invalid',
         classes=['1A Demo Aurora', '2A Demo Aurora'], offset=0),
    dict(key='boreale', code='DEMO_BOREALE', name='Istituto Dimostrativo Boreale (dati finti)', mail='boreale.example.invalid',
         classes=['1B Demo Boreale', '2B Demo Boreale'], offset=12),
]
SUBJECTS = ['Matematica', 'Informatica', 'Inglese']
EXAMS = [  # (class index, subject index == teacher index, title)
    (0, 0, 'Verifica di algebra'), (0, 1, 'Verifica di programmazione'), (1, 2, 'Reading comprehension')]
QUESTIONS = [
    ('Quanto fa 7 x 8?', ['56', '54', '64']), ('Quale numero e primo?', ['13', '15', '21']),
    ('Quanti lati ha un esagono?', ['6', '5', '8']), ('Quanto fa 2 elevato alla 5?', ['32', '16', '64'])]


def slug(text):
    table = str.maketrans({' ': '', "'": ''})
    return text.lower().translate(table)


def people(inst):
    """Deterministic fake people of one institute (names are invented)."""
    o = inst['offset']
    def person(i, role):
        first, last = FIRST[(o + i) % len(FIRST)], LAST[(o + i * 5) % len(LAST)]
        return dict(first=first, last=last, role=role, email=f'{slug(first)}.{slug(last)}.{role.lower()}{i}@{inst["mail"]}')
    admins = [person(0, 'InstitutionAdmin'), person(1, 'InstitutionAdmin')]
    staff = person(2, 'Staff')
    teachers = [person(3 + i, 'Teacher') for i in range(3)]
    students = [[person(6 + c * 6 + i, 'Student') for i in range(6)] for c in range(2)]
    return admins, staff, teachers, students


def student_csv(inst, class_index, roster):
    lines = ['Nome;Cognome;Classe;RiferimentoEsterno']
    for i, p in enumerate(roster, 1):
        lines.append(f"{p['first']};{p['last']};{inst['classes'][class_index]};{inst['code']}-C{class_index + 1}-{i:02d}")
    return ('\n'.join(lines) + '\n').encode('utf-8')


def new_password():
    # API rule: 8-72 bytes with at least one special character.
    return 'Demo-' + secrets.token_urlsafe(15) + '!7'


class Private:
    """Root-only JSON files written atomically with mode 0600."""
    def __init__(self, folder):
        self.folder = Path(folder)
        self.folder.mkdir(mode=0o700, parents=True, exist_ok=True)
        self.folder.chmod(0o700)

    def load(self, name, default):
        path = self.folder / name
        return json.loads(path.read_text(encoding='utf-8')) if path.exists() else default

    def save(self, name, value):
        path = self.folder / name
        tmp = path.with_suffix(path.suffix + '.tmp')
        fd = os.open(tmp, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        with os.fdopen(fd, 'w', encoding='utf-8') as file:
            json.dump(value, file, indent=2, ensure_ascii=False)
            file.write('\n')
        os.chmod(tmp, 0o600)
        os.replace(tmp, path)


class Api:
    def __init__(self, domain, port=443):
        self.domain, self.port = domain, port
        self.context = ssl.create_default_context()
        self.context.minimum_version = ssl.TLSVersion.TLSv1_2
        self.writes = []
        self.counts = {}

    def _throttle(self):
        while True:
            now = time.monotonic()
            self.writes = [t for t in self.writes if now - t < 60]
            if len(self.writes) < WRITE_BUDGET:
                self.writes.append(now)
                return
            time.sleep(61 - (now - self.writes[0]))

    def call(self, method, path, token=None, body=None, raw=None, content_type=None, expect=None):
        for attempt in range(10):
            if method != 'GET':
                self._throttle()
            headers = {'Host': self.domain, 'Accept': 'application/json', 'User-Agent': 'er-demo-seed'}
            data = None
            if raw is not None:
                data, headers['Content-Type'] = raw, content_type
            elif body is not None:
                data, headers['Content-Type'] = json.dumps(body).encode(), 'application/json'
            if token:
                headers['Authorization'] = 'Bearer ' + token
            connection = http.client.HTTPSConnection(self.domain, self.port, context=self.context, timeout=60)
            try:
                connection.request(method, path, body=data, headers=headers)
                response = connection.getresponse()
                payload = response.read()
                status, response_headers = response.status, {k.lower(): v for k, v in response.getheaders()}
            finally:
                connection.close()
            if status == 429:
                # Rejected by the rate limiter before reaching the handler: safe to repeat.
                wait = int(response_headers.get('retry-after', '20') or 20)
                print(f'  rate limit 429 on {method} {route(path)}, attendo {wait}s', flush=True)
                time.sleep(min(max(wait, 5), 70))
                continue
            key = f'{method} {status}'
            self.counts[key] = self.counts.get(key, 0) + 1
            try:
                value = json.loads(payload) if payload else None
            except ValueError:
                value = None
            if expect is not None and status not in (expect if isinstance(expect, tuple) else (expect,)):
                raise RuntimeError(f'{method} {route(path)} -> HTTP {status} (atteso {expect})')
            return status, value, response_headers
        raise RuntimeError(f'{method} {route(path)}: rate limit persistente')


def route(path):
    """Path with ids masked, for logs."""
    return re.sub(r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', '{id}', path.split('?')[0])


def items(value):
    if isinstance(value, dict) and 'items' in value:
        return value['items']
    return value or []


class Owner:
    """tools/OwnerProvisioning, published inside the ops image (it carries the .NET SDK) and run
    with the API image runtime on the staging backend network, as er_migrator over VerifyFull."""
    def __init__(self, folder, state, work):
        self.folder, self.s, self.work = folder, state, Path(work)
        self.project, self.revision = state['project'], state['revision']
        self.tool = self.work / 'owner-build' / 'out'
        self.uuid = None

    def prepare(self):
        build = self.work / 'owner-build'
        if not (self.tool / 'OwnerProvisioning.dll').is_file():
            build.mkdir(mode=0o700, exist_ok=True)
            os.chown(build, 10001, 10001)
            attempt = 'try-' + time.strftime('%Y%m%dT%H%M%S')
            script = ('set -eu; d=/build/' + attempt + '; mkdir -p "$d/src/tools"; '
                      'cp -r /workspace/tools/OwnerProvisioning "$d/src/tools/"; '
                      'cp /workspace/Directory.Build.props /workspace/global.json "$d/src/"; '
                      'export HOME="$d/home" DOTNET_CLI_HOME="$d/home" NUGET_PACKAGES="$d/nuget" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1; '
                      'dotnet restore "$d/src/tools/OwnerProvisioning/OwnerProvisioning.csproj" --locked-mode -v quiet; '
                      'dotnet publish "$d/src/tools/OwnerProvisioning/OwnerProvisioning.csproj" --no-restore -c Release -o "$d/out" -v quiet /p:UseAppHost=false; '
                      'mv "$d/out" /build/out')
            print('  pubblico tools/OwnerProvisioning con l\'immagine ops (SDK .NET)', flush=True)
            host.run(['docker', 'run', '--rm', '--user', '10001:10001', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true',
                      '--label', 'io.er.staging.id=' + self.s['id'], '-v', str(host.ROOT) + ':/workspace:ro', '-v', str(build) + ':/build',
                      '--entrypoint', 'sh', self.project + '-ops:' + self.revision, '-c', script], stdout=subprocess.DEVNULL)
        require((self.tool / 'OwnerProvisioning.dll').is_file(), 'OwnerProvisioning non pubblicato')
        secrets_dir = self.work / 'owner-secrets'
        secrets_dir.mkdir(mode=0o700, exist_ok=True)
        connection = secrets_dir / 'owner-connection'
        if not connection.exists():
            password = (self.folder / 'secrets/db/migrator-password').read_text().strip()
            host.write(connection, 'Server=er-stage-db;Port=3306;Database=electronic_register_staging;User ID=er_migrator;Password=' + password +
                       ';SslMode=VerifyFull;SslCa=/run/secrets/mysql-ca.pem;TlsVersion=TLS 1.2', 1654, 1654, 0o400)
        if not (secrets_dir / 'mysql-ca.pem').exists():
            host.write(secrets_dir / 'mysql-ca.pem', (self.folder / 'pki/ca.pem').read_text(), 1654, 1654, 0o444)
        cid = host.compose(self.folder, 'ps', '-q', 'db', capture=True)
        require(cid, 'Container db della nuova versione assente')
        value = host.output(['docker', 'exec', cid, 'mysql', '--defaults-extra-file=/run/secrets/root.cnf', '--protocol=SOCKET',
                             '--batch', '--skip-column-names', '-e', "SHOW GLOBAL VARIABLES LIKE 'server_uuid'"]).split('\t')[-1]
        require(re.fullmatch(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', value), 'server_uuid inatteso')
        self.uuid = value

    def __call__(self, *args):
        secrets_dir = self.work / 'owner-secrets'
        p = subprocess.run(['docker', 'run', '--rm', '--network', self.project + '_backend', '--user', '1654:1654', '--read-only',
                            '--tmpfs', '/tmp:uid=1654,gid=1654,mode=0700', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true',
                            '-e', 'HOME=/tmp', '-e', 'DOTNET_CLI_TELEMETRY_OPTOUT=1', '--label', 'io.er.staging.id=' + self.s['id'],
                            '-v', str(self.tool) + ':/tool:ro', '-v', str(secrets_dir / 'owner-connection') + ':/run/secrets/owner-connection:ro',
                            '-v', str(secrets_dir / 'mysql-ca.pem') + ':/run/secrets/mysql-ca.pem:ro',
                            '--entrypoint', 'dotnet', self.project + '-api:' + self.revision, '/tool/OwnerProvisioning.dll', *args,
                            '--connection-file', '/run/secrets/owner-connection', '--expected-server-uuid', self.uuid],
                           capture_output=True, text=True, timeout=300)
        lines = dict(line.split(': ', 1) for line in p.stdout.splitlines() if ': ' in line)
        password = next((v for k, v in lines.items() if k.startswith('temporary password (shown once')), None)
        require(p.returncode == 0, f'OwnerProvisioning {args[0]} uscito con codice {p.returncode}')
        return lines, password


class Seeder:
    def __init__(self, folder, state, out, domain, port=443):
        self.folder, self.s, self.domain = folder, state, domain
        self.private = Private(out)
        self.api = Api(domain, port)
        self.ns = uuid.UUID(state['id'])
        self.journal = self.private.load('demo-state.json', dict(stage=state['id'], institutes={}))
        require(self.journal.get('stage') == state['id'], 'Stato demo di un altro staging: usare la cartella della release corrente')
        self.creds = self.private.load('credentials.json', dict(stage=state['id'], accounts={}))
        self.owner = None
        self.tokens = {}

    # ---- persistence -------------------------------------------------------
    def event(self, *parts):
        return str(uuid.uuid5(self.ns, ':'.join(parts)))

    def save(self):
        self.private.save('demo-state.json', self.journal)

    def remember(self, email, **fields):
        self.creds['accounts'].setdefault(email, {}).update(fields)
        self.private.save('credentials.json', self.creds)

    # ---- identity ------------------------------------------------------------
    def login(self, email, password):
        status, body, _ = self.api.call('POST', '/api/Auth/identity/login', body=dict(email=email, password=password))
        return (body if status == 200 else None), status

    def select(self, ticket, institution):
        status, choices, _ = self.api.call('GET', '/api/Auth/institutions', ticket, expect=200)
        mine = [c for c in choices if c['institutionId'] == institution]
        require(len(mine) == 1, 'Membership demo attesa non trovata')
        _, body, _ = self.api.call('POST', '/api/Auth/select-institution', ticket,
                                   dict(institutionId=institution, membershipId=mine[0]['membershipId']), expect=200)
        return body['token']

    def token(self, email):
        cached = self.tokens.get(email)
        if cached and time.monotonic() - cached[1] < TOKEN_MAX_AGE:
            return cached[0]
        account = self.creds['accounts'][email]
        body, status = self.login(email, account['password'])
        require(body and not body.get('passwordChangeRequired'), f'Login demo fallito (HTTP {status})')
        value = self.select(body['ticket'], account['institutionId'])
        self.tokens[email] = (value, time.monotonic())
        return value

    def activate(self, email, temporary, institution):
        """Temporary password -> final password; stores the final one before switching to it."""
        body, status = self.login(email, temporary)
        require(body is not None, f'Login con password temporanea fallito (HTTP {status})')
        if body.get('passwordChangeRequired'):
            final = new_password()
            self.remember(email, pendingFinal=final)
            self.api.call('PUT', '/api/identity/account/password', body['ticket'], dict(currentPassword=temporary, newPassword=final), expect=204)
            self.remember(email, password=final, temporary=None, pendingFinal=None)
        else:
            self.remember(email, password=temporary, temporary=None)
        self.remember(email, institutionId=institution)
        self.tokens.pop(email, None)

    def usable(self, email):
        account = self.creds['accounts'].get(email, {})
        for candidate in (account.get('password'), account.get('pendingFinal')):
            if candidate:
                body, _ = self.login(email, candidate)
                if body and not body.get('passwordChangeRequired'):
                    self.remember(email, password=candidate, pendingFinal=None)
                    return True
        return False

    def recover(self, email, institution):
        """Activate with the stored temporary password, otherwise reset it through the owner script."""
        temporary = self.creds['accounts'].get(email, {}).get('temporary')
        if temporary:
            body, _ = self.login(email, temporary)
            if body:
                return self.activate(email, temporary, institution)
        print('  password temporanea non disponibile: reset con OwnerProvisioning', flush=True)
        _, temporary = self.owner('reset-password', '--email', email, '--reason', 'Demo staging: password temporanea persa')
        require(temporary, 'Reset senza password temporanea')
        self.remember(email, temporary=temporary)
        self.activate(email, temporary, institution)

    # ---- seeding ---------------------------------------------------------------
    def ensure_institution(self, inst, admin):
        record = self.journal['institutes'].setdefault(inst['key'], {})
        lines, temporary = self.owner('create-institution', '--code', inst['code'], '--name', inst['name'], '--admin-email', admin['email'],
                                      '--reason', 'Demo staging (dati finti)')
        record.update(institution=lines['institution'], code=inst['code'], adminMembership=lines['membership'])
        self.save()
        self.remember(admin['email'], institutionId=lines['institution'], role=admin['role'], name=f"{admin['first']} {admin['last']}",
                      institute=inst['name'], **({'temporary': temporary} if temporary else {}))
        if temporary:
            self.activate(admin['email'], temporary, lines['institution'])
        elif not self.usable(admin['email']):
            self.recover(admin['email'], lines['institution'])
        print(f"  {inst['code']}: istituto e primo admin pronti ({lines['status']})", flush=True)
        return record

    def ensure_account(self, actor, record, person, key, **extra):
        institution = record['institution']
        accounts = record.setdefault('accounts', {})
        self.remember(person['email'], institutionId=institution, role=person['role'], name=f"{person['first']} {person['last']}",
                      institute=self.institute_name(record))
        if key in accounts and self.usable(person['email']):
            return accounts[key]
        body = dict(eventId=self.event('account', person['email']), email=person['email'], roles=[person['role']],
                    reason='Demo staging (dati finti)', **extra)
        status, created, headers = self.api.call('POST', '/api/institution/accounts', self.token(actor), body, expect=(200, 201))
        if status == 201:
            require(created.get('temporaryPassword') and headers.get('cache-control') == 'no-store', 'Risposta 201 inattesa')
            self.remember(person['email'], temporary=created['temporaryPassword'])
        accounts[key] = {k: created.get(k) for k in ('userId', 'membershipId', 'teacherId', 'studentId') if created.get(k)}
        if person['role'] == 'Teacher' and 'teacherId' not in accounts[key]:
            _, listed, _ = self.api.call('GET', '/api/institution/teachers', self.token(actor), expect=200)
            found = [x['id'] for x in listed if (x['firstName'], x['lastName']) == (person['first'], person['last'])]
            require(len(found) == 1, 'Scheda docente demo non individuabile')
            accounts[key]['teacherId'] = found[0]
        self.save()
        if status == 201:
            self.activate(person['email'], created['temporaryPassword'], institution)
        elif not self.usable(person['email']):
            self.recover(person['email'], institution)
        return accounts[key]

    def institute_name(self, record):
        return next(i['name'] for i in INSTITUTES if i['code'] == record['code'])

    def find_or_create(self, token, collection, match, body, record, key):
        ids = record.setdefault('ids', {})
        if key in ids:
            return ids[key]
        query = '' if collection == 'subjects' else '?pageNumber=1&pageSize=100'
        _, listed, _ = self.api.call('GET', f'/api/institution/{collection}{query}', token, expect=200)
        found = [x for x in items(listed) if match(x)]
        require(len(found) <= 1, f'{collection}: piu risorse demo con la stessa chiave')
        if found:
            ids[key] = found[0]['id']
        else:
            _, created, _ = self.api.call('POST', f'/api/institution/{collection}', token, body, expect=201)
            ids[key] = created
        self.save()
        return ids[key]

    def structure(self, inst, record, admin, teachers):
        t = self.token(admin)
        b = self.find_or_create(t, 'biennia', lambda x: x['startYear'] == '2026-09-01' and x['endYear'] == '2028-08-31',
                                dict(startYear='2026-09-01', endYear='2028-08-31'), record, 'biennium')
        area = self.find_or_create(t, 'study-areas', lambda x: x['name'] == 'Area tecnologica (demo)',
                                   dict(name='Area tecnologica (demo)', description='Struttura dimostrativa, dati finti'), record, 'area')
        path = self.find_or_create(t, 'study-paths', lambda x: x['name'] == 'Sviluppo software (demo)',
                                   dict(name='Sviluppo software (demo)', description='Percorso dimostrativo'), record, 'path')
        bsa = self.find_or_create(t, 'biennium-study-areas', lambda x: x['bienniumId'] == b and x['studyAreaId'] == area,
                                  dict(bienniumId=b, studyAreaId=area), record, 'bsa')
        bsp = self.find_or_create(t, 'biennium-study-paths', lambda x: x['bienniumStudyAreaId'] == bsa and x['studyPathId'] == path,
                                  dict(bienniumStudyAreaId=bsa, studyPathId=path), record, 'bsp')
        for i, name in enumerate(inst['classes']):
            self.find_or_create(t, 'classes', lambda x, n=name: x['name'] == n, dict(name=name, bienniumStudyPathId=bsp), record, f'class{i}')
        for i, name in enumerate(SUBJECTS):
            self.find_or_create(t, 'subjects', lambda x, n=name: x['name'] == n, dict(name=name), record, f'subject{i}')
        ids = record['ids']
        for c in range(2):
            for s in range(3):
                teacher = record['accounts'][f'teacher{s}']['teacherId']
                self.find_or_create(t, 'class-subjects', lambda x, c=c, s=s: x['classId'] == ids[f'class{c}'] and x['subjectId'] == ids[f'subject{s}'],
                                    dict(classId=ids[f'class{c}'], subjectId=ids[f'subject{s}'], teacherId=teacher), record, f'cs{c}{s}')
        print(f"  {inst['code']}: biennio, area, percorso, 2 classi, 3 materie, 6 assegnazioni docenti", flush=True)

    def import_students(self, inst, record, admin, students):
        """Documented class load: import with EmailMode=None, then one account per student."""
        t = self.token(admin)
        for c in range(2):
            key = f'import{c}'
            done = record.setdefault('imports', {})
            if key not in done:
                content = student_csv(inst, c, students[c])
                digest = hashlib.sha256(content).hexdigest()
                _, history, _ = self.api.call('GET', '/api/institution/imports?page=1&size=100', t, expect=200)
                completed = [b for b in items(history) if str(b.get('fileSha256', '')).lower() == digest and b.get('state') == 'Completed']
                if completed:
                    batch_id = completed[0]['id']
                else:
                    boundary = 'er-demo-' + uuid.uuid4().hex
                    settings = json.dumps(dict(category='Student', emailMode='None'))
                    parsing = json.dumps(dict(delimiter=';', encoding='utf-8'))
                    form = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="studenti.csv"\r\nContent-Type: text/csv\r\n\r\n').encode() + content + \
                           (f'\r\n--{boundary}\r\nContent-Disposition: form-data; name="settings"\r\n\r\n{settings}'
                            f'\r\n--{boundary}\r\nContent-Disposition: form-data; name="parsing"\r\n\r\n{parsing}\r\n--{boundary}--\r\n').encode()
                    _, batch, _ = self.api.call('POST', '/api/institution/imports/preview', t, raw=form,
                                                content_type='multipart/form-data; boundary=' + boundary, expect=201)
                    require(batch['totalRows'] == 6 and batch['validRows'] == 6 and batch['invalidRows'] == 0, 'Anteprima import con righe non valide')
                    batch_id = batch['id']
                    self.api.call('POST', f'/api/institution/imports/{batch_id}/confirm', t,
                                  dict(eventId=self.event('import-confirm', batch_id), expectedRevision=batch['revision'],
                                       previewDigest=batch['previewDigest'], acknowledgeWarnings=True), expect=200)
                _, result, _ = self.api.call('GET', f'/api/institution/imports/{batch_id}/result', t, expect=200)
                rows = sorted(result['rows'], key=lambda r: r['rowNumber'])
                require(len(rows) == 6 and all(r.get('studentId') for r in rows), 'Risultato import incompleto')
                done[key] = [r['studentId'] for r in rows]
                self.save()
            for i, person in enumerate(students[c]):
                self.ensure_account(admin, record, person, f'student{c}{i}', studentId=done[key][i])
        print(f"  {inst['code']}: 12 studenti importati (EmailMode=None) e 12 account Student", flush=True)

    def calendar(self, inst, record, admin):
        body = dict(eventId=self.event('calendar-settings', inst['code']), expectedRevision=-1, timeZoneId='Europe/Rome', approvalLeadDays=7,
                    reason='Demo staging: calendario istituto')
        status, _, _ = self.api.call('PUT', '/api/institution/calendar/settings', self.token(admin), body)
        if status not in (200, 201):
            current, settings, _ = self.api.call('GET', '/api/institution/calendar/settings', self.token(admin))
            require(current == 200 and settings.get('timeZoneId') == 'Europe/Rome', f'Impostazioni calendario non create (HTTP {status})')
        print(f"  {inst['code']}: impostazioni del calendario (Europe/Rome, approvazione 7 giorni)", flush=True)

    def exams(self, inst, record, teachers, students):
        ids, exams = record['ids'], record.setdefault('exams', {})
        for n, (c, s, title) in enumerate(EXAMS):
            teacher = teachers[s]['email']
            name = f'{title} - {inst["classes"][c]}'
            t = self.token(teacher)
            if f'exam{n}' not in exams:
                _, listed, _ = self.api.call('GET', '/api/institution/exams', t, expect=200)
                found = [x for x in listed if x['name'] == name]
                if found:
                    exams[f'exam{n}'] = found[0]['id']
                else:
                    now = datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0)
                    stamp = lambda d: d.isoformat().replace('+00:00', 'Z')
                    roster = record['imports'][f'import{c}']
                    body = dict(classSubjectId=ids[f'cs{c}{s}'], name=name, description='Prova dimostrativa con dati finti',
                                availableFrom=stamp(now - datetime.timedelta(minutes=10)), availableUntil=stamp(now + datetime.timedelta(days=30)),
                                durationMinutes=30, passingPercent=60, assignedStudents=[dict(studentId=x, extraTimeMinutes=0) for x in roster],
                                questions=[dict(position=q + 1, text=text, type=0, points=1, required=True,
                                                options=[dict(position=o + 1, text=opt, isCorrect=o == 0) for o, opt in enumerate(options)])
                                           for q, (text, options) in enumerate(QUESTIONS)])
                    _, created, _ = self.api.call('POST', '/api/institution/exams', t, body, expect=201)
                    exams[f'exam{n}'] = created
                self.save()
            exam_id = exams[f'exam{n}']
            _, detail, _ = self.api.call('GET', f'/api/institution/exams/{exam_id}', t, expect=200)
            if detail['status'] in (0, 'Draft'):
                self.api.call('POST', f'/api/institution/exams/{exam_id}/publish', t, expect=204)
            questions = sorted(detail['questions'], key=lambda q: q.get('position', 0))
            for i, person in enumerate(students[c][:SUBMITTERS]):
                self.submit(person['email'], exam_id, questions, correct=(i + n) % 4 + 1)
        print(f"  {inst['code']}: 3 esami pubblicati, {SUBMITTERS} consegne ciascuno con voto ufficiale", flush=True)

    def submit(self, email, exam_id, questions, correct):
        t = self.token(email)
        _, assigned, _ = self.api.call('GET', '/api/institution/exams/assigned', t, expect=200)
        mine = next(x for x in assigned if x['id'] == exam_id)
        if mine.get('submittedAt'):
            return
        if mine.get('submissionId'):
            submission = mine['submissionId']
        else:
            _, started, _ = self.api.call('POST', f'/api/institution/exams/{exam_id}/start', t, expect=200)
            submission = started['submissionId']
        _, session, _ = self.api.call('GET', f'/api/institution/submissions/{submission}/session', t, expect=200)
        answers = []
        for q, question in enumerate(questions):
            options = sorted(question['options'], key=lambda o: o.get('position', 0))
            choice = options[0] if q < correct else options[1]
            answers.append(dict(questionId=question['id'], selectedOptionIds=[choice['id']]))
        revision = int(session.get('currentRevision', 0)) + 1
        self.api.call('PUT', f'/api/institution/submissions/{submission}/answers', t, dict(clientRevision=revision, answers=answers), expect=200)
        self.api.call('POST', f'/api/institution/submissions/{submission}/submit', t, dict(clientRevision=revision + 1, answers=answers), expect=200)

    def run(self):
        self.owner = Owner(self.folder, self.s, self.private.folder)
        self.owner.prepare()
        for inst in INSTITUTES:
            admins, staff, teachers, students = people(inst)
            print(f"== {inst['name']}", flush=True)
            record = self.ensure_institution(inst, admins[0])
            a = admins[0]['email']
            self.ensure_account(a, record, admins[1], 'admin1')
            self.ensure_account(a, record, staff, 'staff')
            for i, person in enumerate(teachers):
                self.ensure_account(a, record, person, f'teacher{i}', firstName=person['first'], lastName=person['last'])
            print(f"  {inst['code']}: 2 InstitutionAdmin (il secondo creato dal primo), 1 Staff, 3 Teacher", flush=True)
            self.structure(inst, record, a, teachers)
            self.import_students(inst, record, a, students)
            self.calendar(inst, record, a)
            self.exams(inst, record, teachers, students)
        self.write_summary()
        print(json.dumps(dict(demoSeed='OK', institutes=len(INSTITUTES), accounts=len(self.creds['accounts']), http=self.api.counts)), flush=True)

    def write_summary(self):
        lines = ['Credenziali demo dello staging (dati finti). File privato: non copiarlo nel repository o nei log.',
                 f'Staging {self.s["project"]}, https://{self.domain}', '']
        for inst in INSTITUTES:
            lines.append(f"== {inst['name']} (codice {inst['code']})")
            for email, a in self.creds['accounts'].items():
                if a.get('institute') == inst['name']:
                    lines.append(f"{a['role']:<17} {a['name']:<24} {email:<56} {a.get('password') or '(password non impostata)'}")
            lines.append('')
        path = self.private.folder / 'credenziali-demo.txt'
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        with os.fdopen(fd, 'w', encoding='utf-8') as file:
            file.write('\n'.join(lines))
        os.chmod(path, 0o600)


class Verifier(Seeder):
    def check(self, name, ok, detail=''):
        self.results.append(dict(check=name, ok=bool(ok), detail=detail))
        print(('PASS ' if ok else 'FAIL ') + name + (f' ({detail})' if detail else ''), flush=True)

    def run(self):
        self.results = []
        accounts = self.creds['accounts']
        self.check('credenziali demo presenti per entrambi gli istituti', len(accounts) == 2 * 18, f'account={len(accounts)}')
        refused = {}
        for email, account in accounts.items():
            body, status = self.login(email, account.get('password') or 'missing-password!')
            if not (body and not body.get('passwordChangeRequired')):
                refused[str(status)] = refused.get(str(status), 0) + 1
        self.check('ogni account demo entra con la password finale, senza cambio richiesto', not refused, json.dumps(refused))
        records = {inst['key']: self.journal['institutes'].get(inst['key'], {}) for inst in INSTITUTES}
        for inst in INSTITUTES:
            record = records[inst['key']]
            admins, staff, teachers, students = people(inst)
            t = self.token(admins[0]['email'])
            _, context, _ = self.api.call('GET', '/api/Auth/context', t, expect=200)
            self.check(f"{inst['code']}: admin nel proprio istituto", context['institutionId'] == record['institution'] and context['roles'] == ['InstitutionAdmin'])
            _, classes, _ = self.api.call('GET', '/api/institution/classes?pageNumber=1&pageSize=100', t, expect=200)
            names = {x['name'] for x in items(classes)}
            self.check(f"{inst['code']}: 2 classi demo", set(inst['classes']) <= names)
            for c in range(2):
                _, roster, _ = self.api.call('GET', f"/api/institution/students?classId={record['ids'][f'class{c}']}", t, expect=200)
                expected = {(p['first'], p['last']) for p in students[c]}
                self.check(f"{inst['code']}: classe {inst['classes'][c]} con 6 studenti importati", {(x['firstName'], x['lastName']) for x in roster} == expected, f'studenti={len(roster)}')
            _, teacher_list, _ = self.api.call('GET', '/api/institution/teachers', t, expect=200)
            self.check(f"{inst['code']}: 3 docenti", {record['accounts'][f'teacher{i}']['teacherId'] for i in range(3)} <= {x['id'] for x in teacher_list})
            _, assignments, _ = self.api.call('GET', '/api/institution/class-subjects?pageNumber=1&pageSize=100', t, expect=200)
            mine = {record['ids'][f'cs{c}{s}'] for c in range(2) for s in range(3)}
            self.check(f"{inst['code']}: 6 assegnazioni docente-materia-classe", mine <= {x['id'] for x in items(assignments)})
            _, members, _ = self.api.call('GET', '/api/institution/admin/members?pageNumber=1&pageSize=100', t, expect=200)
            roles = [r for m in items(members) for r in m.get('roles', [])]
            self.check(f"{inst['code']}: ruoli 2 InstitutionAdmin, 1 Staff, 3 Teacher, 12 Student",
                       roles.count('InstitutionAdmin') >= 2 and roles.count('Staff') >= 1 and roles.count('Teacher') >= 3 and roles.count('Student') >= 12,
                       f"admin={roles.count('InstitutionAdmin')} staff={roles.count('Staff')} teacher={roles.count('Teacher')} student={roles.count('Student')}")
            _, settings, _ = self.api.call('GET', '/api/institution/calendar/settings', t, expect=200)
            self.check(f"{inst['code']}: calendario configurato", settings.get('timeZoneId') == 'Europe/Rome')
            graded = 0
            for n in range(len(EXAMS)):
                _, subs, _ = self.api.call('GET', f"/api/institution/exams/{record['exams'][f'exam{n}']}/submissions", t, expect=200)
                graded += sum(1 for x in subs if x.get('submittedAt') and x.get('gradeId'))
            self.check(f"{inst['code']}: 3 esami con consegne valutate", graded == len(EXAMS) * SUBMITTERS, f'consegne={graded}')
            _, grades, _ = self.api.call('GET', '/api/institution/grades', t, expect=200)
            student_ids = {x for c in range(2) for x in record['imports'][f'import{c}']}
            self.check(f"{inst['code']}: voti ufficiali registrati", sum(1 for g in grades if g['studentId'] in student_ids) >= len(EXAMS) * SUBMITTERS, f'voti={len(grades)}')
        self.isolation(records)
        failed = [r for r in self.results if not r['ok']]
        self.private.save('demo-verify.json', dict(stage=self.s['id'], results=self.results))
        print(json.dumps(dict(demoVerify='OK' if not failed else 'FAIL', PASS=len(self.results) - len(failed), FAIL=len(failed))), flush=True)
        require(not failed, 'Verifica dei dati demo fallita')

    def isolation(self, records):
        for inst, other in ((INSTITUTES[0], INSTITUTES[1]), (INSTITUTES[1], INSTITUTES[0])):
            mine, theirs = records[inst['key']], records[other['key']]
            label = f"{inst['code']} -> {other['code']}"
            admins, _, teachers, students = people(inst)
            t = self.token(admins[0]['email'])
            probes = [('classes', theirs['ids']['class0']), ('students', theirs['imports']['import0'][0]),
                      ('teachers', theirs['accounts']['teacher0']['teacherId']), ('exams', theirs['exams']['exam0']),
                      ('class-subjects', theirs['ids']['cs00']), ('subjects', theirs['ids']['subject0'])]
            codes = {name: self.api.call('GET', f'/api/institution/{name}/{rid}', t)[0] for name, rid in probes}
            self.check(f'isolamento {label}: risorse dell\'altro istituto non visibili (404)', all(v == 404 for v in codes.values()), json.dumps(codes))
            _, lists, _ = self.api.call('GET', '/api/institution/students', t, expect=200)
            foreign = {x for c in range(2) for x in theirs['imports'][f'import{c}']}
            self.check(f'isolamento {label}: elenco studenti senza studenti dell\'altro istituto', not foreign & {x['id'] for x in lists})
            _, page, _ = self.api.call('GET', f"/api/institution/grades/paged?studentId={theirs['imports']['import0'][0]}", t, expect=200)
            self.check(f'isolamento {label}: nessun voto dell\'altro istituto', not items(page))
            account = self.creds['accounts'][admins[0]['email']]
            body, _ = self.login(admins[0]['email'], account['password'])
            _, choices, _ = self.api.call('GET', '/api/Auth/institutions', body['ticket'], expect=200)
            self.check(f'isolamento {label}: l\'admin vede solo il proprio istituto', [c['institutionId'] for c in choices] == [mine['institution']])
            status = self.api.call('POST', '/api/Auth/select-institution', body['ticket'],
                                   dict(institutionId=theirs['institution'], membershipId=theirs['adminMembership']))[0]
            self.check(f'isolamento {label}: selezione dell\'altro istituto rifiutata', status in (403, 404), f'HTTP {status}')
            student = self.token(students[0][0]['email'])
            _, assigned, _ = self.api.call('GET', '/api/institution/exams/assigned', student, expect=200)
            self.check(f'isolamento {label}: lo studente non vede esami dell\'altro istituto',
                       not {theirs['exams'][f'exam{n}'] for n in range(len(EXAMS))} & {x['id'] for x in assigned})
            teacher = self.token(teachers[0]['email'])
            status = self.api.call('GET', f"/api/institution/exams/{theirs['exams']['exam0']}", teacher)[0]
            self.check(f'isolamento {label}: il docente non apre esami dell\'altro istituto', status == 404, f'HTTP {status}')


def login_probe(out, domain, ca_file=None):
    """Read-only HTTPS probe with the stored demo credentials (no Docker, no state folder):
    the first admin of each institute logs in without a password change and sees one institution."""
    api = Api(domain)
    if ca_file:
        api.context = ssl.create_default_context(cafile=ca_file)
    creds = json.loads((Path(out) / 'credentials.json').read_text(encoding='utf-8'))['accounts']
    for inst in INSTITUTES:
        email = people(inst)[0][0]['email']
        status, body, _ = api.call('POST', '/api/Auth/identity/login', body=dict(email=email, password=creds[email]['password']))
        require(status == 200 and body['passwordChangeRequired'] is False, f'login {inst["code"]}: HTTP {status}')
        _, choices, _ = api.call('GET', '/api/Auth/institutions', body['ticket'], expect=200)
        require([c['institutionId'] for c in choices] == [creds[email]['institutionId']], f'{inst["code"]}: istituti visibili inattesi')
        print(f'PASS login demo {inst["code"]} via HTTPS ({sys.version.split()[0]}, {ssl.OPENSSL_VERSION})', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['seed', 'verify', 'login-probe'])
    parser.add_argument('--state', help='state folder of the NEW staging (seed, verify)')
    parser.add_argument('--out', required=True, help='private folder (0700, outside the checkout) for credentials and progress')
    parser.add_argument('--domain', default='staging.consoliprojects.dev')
    parser.add_argument('--ca-file', help='login-probe only: CA bundle instead of the system trust store')
    args = parser.parse_args()
    require(sys.platform.startswith('linux') and os.geteuid() == 0, 'Eseguire come root sull\'host di staging')
    if args.command == 'login-probe':
        return login_probe(Path(args.out), args.domain, args.ca_file)
    require(args.state, '--state obbligatorio')
    folder, state = host.load(args.state)
    out = Path(args.out).resolve()
    require(out != host.ROOT and host.ROOT not in out.parents, 'Le credenziali demo devono stare fuori dal checkout')
    host.owned(state)
    host.check_images(folder, state)
    (Verifier if args.command == 'verify' else Seeder)(folder, state, out, args.domain).run()


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, subprocess.CalledProcessError, subprocess.TimeoutExpired, OSError, KeyError, TypeError, ValueError, StopIteration) as error:
        print('Demo ' + type(error).__name__ + ': ' + str(error), file=sys.stderr)
        sys.exit(1)
