"""Host-side staging preparation/control. No SSH, Nginx edits, deletion or DDL."""
import argparse
import hashlib
import http.client
import ipaddress
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
COMPOSE = ROOT / 'infra/staging/compose.yaml'
MYSQL = json.loads((ROOT / 'infra/staging/mysql-image.json').read_text())

def require(value, message):
    if not value:
        raise RuntimeError(message)

def run(args, **kwargs):
    return subprocess.run([str(a) for a in args], check=True, text=True, **kwargs)

def output(args):
    return subprocess.check_output([str(a) for a in args], text=True).strip()

def digest(path):
    return hashlib.sha256(Path(path).read_bytes().replace(b'\r\n', b'\n')).hexdigest().upper()

def write(path, content, uid=0, gid=0, mode=0o400):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    with path.open('x', encoding='utf-8', newline='\n') as file:
        file.write(content)
    os.chown(path, uid, gid)
    path.chmod(mode)

def validate(domain, subnet, port):
    require(len(domain)<=253 and '.' in domain and all(re.fullmatch(r'[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?',label) for label in domain.split('.')), 'Use an explicit DNS hostname')
    require(domain != 'consoliprojects.dev', 'Use a separate staging subdomain; preserve the existing apex vhost')
    net = ipaddress.ip_network(subnet)
    require(net.version == 4 and net.prefixlen == 24 and net.is_private and not net.is_loopback and not net.is_link_local, 'A private /24 is required')
    require(1024 <= port <= 65535 and port not in (3306, 13306), 'Use a separate unprivileged API port')
    return net

def environment(state, folder):
    return '\n'.join(f'{k}={v}' for k, v in {
        'COMPOSE_PROJECT_NAME': state['project'], 'STAGING_ID': state['id'], 'STATE_DIR': str(folder),
        'STAGING_SUBNET': state['subnet'], 'STAGING_GATEWAY': state['gateway'], 'SQL_HOST_PATTERN': state['sql_host'],
        'API_PORT': state['port'], 'MYSQL_IMAGE': state['mysql_image'], 'SOURCE_REVISION': state['revision'],
        'API_IMAGE': f"{state['project']}-api:{state['revision']}", 'OPS_IMAGE': f"{state['project']}-ops:{state['revision']}"
    }.items()) + '\n'

def nginx(state, tls):
    domain, project, port = state['domain'], state['project'], state['port']
    http = f'''server {{
    listen 80;
    server_name {domain};
    location ^~ /.well-known/acme-challenge/ {{ root /var/lib/er-stage-acme/{project}; }}
    location / {{ return 308 https://$host$request_uri; }}
}}
'''
    if not tls:
        return http
    return f'''log_format er_stage_{state['id']} '$remote_addr [$time_local] "$request_method $uri $server_protocol" $status $body_bytes_sent $request_time';
server {{
    listen 443 ssl;
    server_name {domain};
    ssl_certificate /etc/nginx/{project}/fullchain.pem;
    ssl_certificate_key /etc/nginx/{project}/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    access_log /var/log/er-stage/{project}/access.log er_stage_{state['id']};
    error_log /var/log/er-stage/{project}/error.log error;
    client_max_body_size 1m;
    location / {{
        proxy_pass http://127.0.0.1:{port};
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_read_timeout 60s;
    }}
}}
'''

def runtime_configuration(domain, gateway):
    """Runtime for the isolated Docker staging only."""
    return {'Deployment': {'Mode':'Staging','RolloutApproved':True}, 'urls':'http://0.0.0.0:8080', 'AllowedHosts':domain,
               'Jwt':{'Issuer':'er-staging','Audience':'er-staging-client'},
               'Readiness':{'ExpectedVersion':MYSQL['version'],'ExpectedHost':'er-stage-db','ExpectedDataDir':'/var/lib/mysql'},
               'Transport':{'KnownProxies':[gateway,'127.0.0.1'], 'AllowedOrigins':['https://consoliprojects.dev'], 'HttpsPort':443},
               'Identity':{'Legacy':{'Enabled':False}},
               'Multitenancy':{k:{'Enabled':k in ('SchoolApi','CourseApi','ExamApi','MembershipAdminApi','MembershipWriteApi','InstitutionCalendar')} for k in ('SchoolApi','CourseApi','ExamApi','MembershipAdminApi','MembershipWriteApi','InstitutionCalendar','Onboarding','Governance','InstitutionWorker')}}

def prepare(args):
    require(sys.platform.startswith('linux') and os.geteuid() == 0, 'Prepare on Linux as root to set real bind-secret ownership; no permission bypass')
    require(shutil.which('openssl'), 'openssl is required')
    folder = Path(args.state).resolve()
    require(folder!=ROOT and ROOT not in folder.parents,'Keep secrets/state OUTSIDE the checkout and build context')
    require(not folder.exists() and not any(c in str(folder) for c in '\n\r $\"\''), 'Use a NEW absolute state directory without shell metacharacters/spaces')
    net = validate(args.domain, args.subnet, args.port)
    require(re.fullmatch(r'[a-zA-Z0-9_./:-]+@sha256:[a-f0-9]{64}',args.mysql_image),'Use an explicit image reference pinned by sha256 digest without whitespace/metacharacters')
    require(args.mysql_image == MYSQL['image'], 'Only the reviewed staging image is allowed; update mysql-image.json and its registry evidence before selecting another image')
    revision = output(['git', '-c', 'safe.directory='+str(ROOT), '-C', ROOT, 'rev-parse', 'HEAD'])
    dirty = bool(output(['git', '-c', 'safe.directory='+str(ROOT), '-C', ROOT, 'status', '--porcelain', '--untracked-files=normal']))
    identity = uuid.uuid4().hex
    state = dict(format=3, mysql_version=MYSQL['version'], id=identity, project='er-stage-'+identity[:12], domain=args.domain,
                 subnet=str(net), gateway=str(net.network_address+1), sql_host=str(net.network_address).rsplit('.', 1)[0]+'.%',
                 port=args.port, mysql_image=args.mysql_image, revision=revision, source_dirty=dirty,
                 manifest=digest(ROOT/'migrations/release/manifest.json'), repository=str(ROOT))
    folder.mkdir(mode=0o700, parents=True)
    for name in ('public', 'ops', 'pki', 'secrets/db', 'secrets/api', 'secrets/ops'):
        (folder/name).mkdir(parents=True, mode=0o700)
    (folder/'public').chmod(0o755)
    os.chown(folder/'ops', 10001, 10001)
    write(folder/'state.json', json.dumps(state, indent=2)+'\n')
    write(folder/'.env', environment(state, folder), mode=0o600)
    passwords = {name: secrets.token_hex(32) for name in ('root', 'migrator', 'api', 'review')}
    jwt = secrets.token_hex(48)
    pki = folder/'pki'
    def openssl(*items):
        with (pki/'generation.log').open('a') as log:
            run(['openssl', *items], stdout=log, stderr=log)
    openssl('req', '-x509', '-newkey', 'rsa:3072', '-sha256', '-nodes', '-keyout', pki/'ca-key.pem', '-out', pki/'ca.pem', '-days', '365', '-addext', 'basicConstraints=critical,CA:TRUE', '-addext', 'keyUsage=critical,keyCertSign,cRLSign', '-subj', '/CN=ER staging '+identity)
    openssl('req', '-new', '-newkey', 'rsa:3072', '-sha256', '-nodes', '-keyout', pki/'server-key.pem', '-out', pki/'server.csr', '-subj', '/CN=er-stage-db')
    write(pki/'server.ext', 'basicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\nsubjectAltName=DNS:er-stage-db\n')
    openssl('x509', '-req', '-in', pki/'server.csr', '-CA', pki/'ca.pem', '-CAkey', pki/'ca-key.pem', '-CAcreateserial', '-out', pki/'server.pem', '-days', '90', '-sha256', '-extfile', pki/'server.ext')
    openssl('req', '-x509', '-newkey', 'rsa:3072', '-sha256', '-nodes', '-keyout', pki/'wrong-ca-key.pem', '-out', pki/'wrong-ca.pem', '-days', '30', '-addext', 'basicConstraints=critical,CA:TRUE', '-addext', 'keyUsage=critical,keyCertSign,cRLSign', '-subj', '/CN=Unrelated staging negative CA')
    for name, source, uid, gid, mode in [
        ('db/ca.pem', 'ca.pem', 999, 999, 0o400), ('db/server.pem', 'server.pem', 999, 999, 0o400),
        ('db/server-key.pem', 'server-key.pem', 999, 999, 0o400), ('api/ca.pem', 'ca.pem', 1654, 1654, 0o400),
        ('ops/ca.pem', 'ca.pem', 10001, 10001, 0o400), ('ops/wrong-ca.pem', 'wrong-ca.pem', 10001, 10001, 0o400)]:
        write(folder/'secrets'/name, (pki/source).read_text(), uid, gid, mode)
    write(folder/'secrets/db/root-password', passwords['root'], 999, 999)
    write(folder/'secrets/db/root.cnf', '[client]\nuser=root\npassword='+passwords['root']+'\nsocket=/var/run/mysqld/mysqld.sock\n', 999, 999)
    write(folder/'secrets/db/migrator-password', passwords['migrator'], 999, 10001, 0o440)
    for name, user in [('migrator', 'er_migrator'), ('review', 'er_review')]:
        write(folder/f'secrets/ops/{"reviewer" if name=="review" else name}.cnf', f'[client]\nuser={user}\npassword={passwords[name]}\nhost=er-stage-db\nport=3306\nprotocol=TCP\nssl-mode=VERIFY_IDENTITY\nssl-ca=/run/secrets/mysql-ca.pem\ntls-version=TLSv1.2\n', 10001, 10001)
    for name in ('api', 'review'):
        write(folder/f'secrets/ops/{name}-password', passwords[name], 10001, 10001)
    for name, uid in [('api', 1654), ('ops', 10001)]:
        write(folder/f'secrets/{name}/jwt-key', jwt, uid, uid)
    connection = 'Server=er-stage-db;Port=3306;Database=electronic_register_staging;User ID=er_api;Password='+passwords['api']+';SslMode=VerifyFull;SslCa=/run/secrets/mysql-ca.pem;TlsVersion=TLS 1.2'
    write(folder/'secrets/api/connection', connection, 1654, 1654)
    runtime = runtime_configuration(args.domain, state['gateway'])
    public = dict(state)
    write(folder/'public/staging.json', json.dumps(public, indent=2), mode=0o444)
    write(folder/'public/runtime.json', json.dumps(runtime, indent=2), mode=0o444)
    write(folder/'public/binding.json', json.dumps(dict(mysqlSeries='8.4',artifactRoot='/opt/release',database='electronic_register_staging',definerUser='er_stage_definer',definerHost='%',apiUser='er_api',apiHost=state['sql_host'],reviewUser='er_review',reviewHost=state['sql_host']),indent=2), mode=0o444)
    write(folder/'public/preflight.json', json.dumps(dict(host='er-stage-db',port=3306,database='electronic_register_staging',user='er_review',passwordFile='/run/secrets/review-password',caFile='/run/secrets/mysql-ca.pem',expectedVersion=MYSQL['version'],expectedDataDir='/var/lib/mysql',schemaDefiner='er_stage_definer@%',mysqlClient='mysql'),indent=2), mode=0o444)
    seed = {'stage_id':identity,'ids':{k:str(uuid.uuid4()) for k in ('tenant_a','tenant_b','biennium_a','biennium_b','area_a','area_b','path_a','path_b','bsa_a','bsa_b','bsp_a','bsp_b','class_a','class_b','teacher','student','subject','assignment')},'accounts':{}}
    for name, role, tenant in [('admin_a','InstitutionAdmin','a'),('admin_b','InstitutionAdmin','b'),('teacher','Teacher','a'),('student','Student','a')]:
        seed['accounts'][name]=dict(id=str(uuid.uuid4()),membership=str(uuid.uuid4()),role=role,tenant=tenant,email=f'{identity}.{name}@example.invalid',password=secrets.token_hex(24)+'aA!9')
    write(folder/'ops/seed.json', json.dumps(seed, indent=2), 10001, 10001)
    write(folder/'nginx-http.conf', nginx(state, False), mode=0o600)
    write(folder/'nginx-https.conf', nginx(state, True), mode=0o600)
    write(folder/'nginx-logrotate.conf',f'/var/log/er-stage/{state["project"]}/*.log {{\n  daily\n  rotate 7\n  compress\n  delaycompress\n  missingok\n  notifempty\n  copytruncate\n}}\n',mode=0o600)
    print(json.dumps({'prepared':str(folder),'project':state['project'],'sourceDirty':dirty,'dockerVerified':False,'tlsVerified':False}))

def load(folder):
    folder = Path(folder).resolve()
    state = json.loads((folder/'state.json').read_text())
    require(state.get('format') == 3 and state.get('mysql_version') == MYSQL['version'] and state.get('mysql_image') == MYSQL['image'],
            'Incompatible staging state/image: prepare a NEW isolated state and volume; never upgrade/reuse older MySQL volumes')
    require(re.fullmatch(r'[a-f0-9]{32}',state['id']) and state['project']=='er-stage-'+state['id'][:12], 'Invalid staging ownership record')
    require(state['repository']==str(ROOT), 'Use the recorded checkout; create new staging for another checkout')
    return folder,state

def compose(folder, *args, capture=False):
    cmd=['docker','compose','--env-file',folder/'.env','-f',COMPOSE,*args]
    return output(cmd) if capture else run(cmd)

def owned(state):
    for kind, names in [('volume',[state['project']+'_db-data']),('network',[state['project']+'_backend'])]:
        for name in names:
            check=subprocess.run(['docker',kind,'inspect',name],capture_output=True,text=True)
            if check.returncode==0:
                obj=json.loads(check.stdout)[0]
                require(obj.get('Labels',{}).get('io.er.staging.id')==state['id'], 'Foreign existing '+kind+': '+name)
    ids=output(['docker','ps','-aq','--filter','label=com.docker.compose.project='+state['project']]).splitlines()
    for cid in ids:
        obj=json.loads(output(['docker','inspect',cid]))[0]
        # Every service, including the short-lived initializer, must carry ownership.
        labels=obj['Config']['Labels']
        require(labels.get('io.er.staging.id')==state['id'], 'Foreign container in staging project')

def clean(state):
    require(not state['source_dirty'] and not output(['git','-c','safe.directory='+str(ROOT),'-C',ROOT,'status','--porcelain','--untracked-files=normal']), 'Build/start requires clean PUBLISHED code; no automatic commit/push')
    require(output(['git','-c','safe.directory='+str(ROOT),'-C',ROOT,'rev-parse','HEAD'])==state['revision'] and digest(ROOT/'migrations/release/manifest.json')==state['manifest'], 'Checkout differs from prepared release')


def check_mysql_tls(help_text):
    # Inspect the binary's compiled defaults, not an option we pass ourselves.
    match = re.search(r'^tls-version\s+(\S+)\s*$', help_text, re.M)
    require(match and 'TLSv1.2' in match[1].split(','),
            'MySQL binary lacks compiled TLSv1.2 support. '
            'Staging requires the reviewed MySQL 8.4 LTS OpenSSL image in a NEW state; '
            'do not lower TLS policy. Runtime TLS/identity probes remain mandatory.')
    return match[1]


def check_mysql_version(version_text):
    require(re.search(r'\bVer\s+' + re.escape(MYSQL['version']) + r'\s', version_text), 'Wrong MySQL binary: exact staging version required')

def check_mysql_openssl(linkage):
    require('libssl.so' in linkage and 'libcrypto.so' in linkage and 'not found' not in linkage,
            'MySQL server OpenSSL linkage unavailable')
    # ldd addresses vary with ASLR; capability evidence must survive a repeated build.
    return re.sub(r'0x[0-9a-fA-F]+','<address>',linkage)

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--state',required=True)
    p.add_argument('command',choices=['prepare','config','build','db','sql-check','provision','compatibility','seed','preflight','tls','start','verify','http','smoke','logs','diagnostics','stop','restart'])
    p.add_argument('--domain',default='staging.consoliprojects.dev')
    p.add_argument('--subnet',default='172.30.252.0/24')
    p.add_argument('--port',type=int,default=18080)
    # Registry provenance in staging-evidence/mysql-8.4-registry.json.
    # Only this reviewed digest is allowed; runtime checks remain mandatory.
    p.add_argument('--mysql-image',default=MYSQL['image'])
    a=p.parse_args()
    if a.command=='prepare':return prepare(a)
    folder,s=load(a.state)
    require(sys.platform.startswith('linux'), 'Docker staging control is for the Ubuntu host')
    require(os.environ.get('DOCKER_HOST','unix:///var/run/docker.sock')=='unix:///var/run/docker.sock' and os.environ.get('DOCKER_CONTEXT','default')=='default','Only the local rootful Docker daemon is allowed; no SSH/remote context')
    context=json.loads(output(['docker','context','inspect','default']))[0]
    require(context['Endpoints']['docker']['Host']=='unix:///var/run/docker.sock','Unexpected Docker endpoint')
    owned(s)
    if a.command=='diagnostics':
        # No clean-source/build requirement: diagnostics must work on failed builds.
        from diagnostics import collect
        return collect(folder, s)
    if a.command=='config':return compose(folder,'config','--quiet')
    if a.command=='logs':return compose(folder,'logs','--tail','100','api','db')
    if a.command=='stop':return compose(folder,'stop','api','db')
    clean(s)
    require(tuple(map(int,output(['docker','version','--format','{{.Server.Version}}']).split('-')[0].split('.')[:2]))>=(28,0),'Docker Engine >=28 required for loopback publication isolation')
    require(tuple(map(int,output(['docker','compose','version','--short']).lstrip('v').split('-')[0].split('.')[:2]))>=(2,24),'Docker Compose >=2.24 required')
    if a.command=='build':
        require(output(['uname','-m'])=='x86_64','MySQL staging image is amd64; do not silently emulate/upgrade on Graviton')
        run(['docker','pull','--platform',MYSQL['platform'],s['mysql_image']])
        db_image=json.loads(output(['docker','image','inspect',s['mysql_image']]))[0]
        require(db_image['Os']=='linux' and db_image['Architecture']=='amd64','Unexpected MySQL image platform')
        for flag, expected in [('-u',MYSQL['uid']),('-g',MYSQL['gid'])]:
            require(output(['docker','run','--rm','--network','none','--entrypoint','id',s['mysql_image'],flag,'mysql'])==expected,'Unexpected MySQL UID/GID')
        version=output(['docker','run','--rm','--network','none','--entrypoint','mysqld',s['mysql_image'],'--version'])
        check_mysql_version(version)
        # Inspect the actual pinned server binary, not a SQL variable or the client library.
        linkage=output(['docker','run','--rm','--network','none','--read-only',
                        '--user','999:999','--cap-drop','ALL','--security-opt','no-new-privileges:true',
                        '--entrypoint','sh',s['mysql_image'],'-ec','ldd "$(command -v mysqld)"'])
        linkage=check_mysql_openssl(linkage)
        compiled_tls=check_mysql_tls(output(['docker','run','--rm','--network','none','--read-only',
                               '--user','999:999','--cap-drop','ALL',
                               '--security-opt','no-new-privileges:true',
                               '--entrypoint','mysqld',s['mysql_image'],
                               '--no-defaults','--verbose','--help']))
        capabilities=dict(image=s['mysql_image'],imageId=db_image['Id'],platform=MYSQL['platform'],
                          versionOutput=version,uid=MYSQL['uid'],gid=MYSQL['gid'],compiledTls=compiled_tls,
                          opensslLinkage=linkage,runtimeTlsVerified=False)
        cap_file=folder/'mysql-capabilities.json'
        if cap_file.exists():require(json.loads(cap_file.read_text())==capabilities,'MySQL capabilities changed: prepare a new state')
        else:write(cap_file,json.dumps(capabilities,indent=2))
        compose(folder,'build','api','ops')
        api=json.loads(output(['docker','image','inspect',s['project']+'-api:'+s['revision']]))[0]
        require(api['Config']['Labels'].get('org.opencontainers.image.revision')==s['revision'],'API image revision label mismatch')
        compose(folder,'run','--rm','--no-deps','ops','source')
        images={k:json.loads(output(['docker','image','inspect',tag]))[0]['Id'] for k,tag in [('db',s['mysql_image']),('api',s['project']+'-api:'+s['revision']),('ops',s['project']+'-ops:'+s['revision'])]}
        dest=folder/'images.json'
        if dest.exists():require(json.loads(dest.read_text())==images,'Images changed: use a new state, never silently replace the staged artifacts')
        else:write(dest,json.dumps(images,indent=2))
        return
    require((folder/'images.json').exists(),'Build and record images first')
    images=json.loads((folder/'images.json').read_text())
    for k,tag in [('db',s['mysql_image']),('api',s['project']+'-api:'+s['revision']),('ops',s['project']+'-ops:'+s['revision'])]:
        require(json.loads(output(['docker','image','inspect',tag]))[0]['Id']==images[k], 'Image drift: '+k)
    if a.command=='db':
        if subprocess.run(['docker','network','inspect',s['project']+'_backend'],capture_output=True).returncode!=0:
            candidate=ipaddress.ip_network(s['subnet'])
            networks=output(['docker','network','ls','-q']).splitlines()
            for name in networks:
                for cfg in json.loads(output(['docker','network','inspect',name]))[0].get('IPAM',{}).get('Config',[]):
                    if 'Subnet' in cfg and ':' not in cfg['Subnet']:require(not candidate.overlaps(ipaddress.ip_network(cfg['Subnet'])),'Docker subnet collision')
            for route in json.loads(output(['ip','-json','route'])):
                if route.get('dst','default')!='default' and ':' not in route['dst']:require(not candidate.overlaps(ipaddress.ip_network(route['dst'],strict=False)), 'Host/VPC route collision')
        return compose(folder,'up','-d','--wait','--wait-timeout','180','db')
    if a.command=='sql-check':
        compose(folder,'exec','-T','db','sh','/opt/er/db-health.sh')
        return compose(folder,'run','--rm','--no-deps','ops','identity')
    if a.command in ('provision','compatibility','seed','preflight','tls','smoke'):
        return compose(folder,'run','--rm','--no-deps','ops',a.command)
    if a.command in ('start','restart'):
        if a.command=='restart':compose(folder,'up','-d','--wait','--wait-timeout','180','db')
        with socket.socket() as probe:
            # On a restart the owned API may already hold the port; inspect it below.
            api_ids=compose(folder,'ps','-q','api',capture=True)
            if not api_ids:probe.bind(('127.0.0.1',s['port']))
        compose(folder,'run','--rm','--no-deps','ops','preflight')
        compose(folder,'up','-d','--wait','--wait-timeout','90','api')
        return verify(folder,s)
    if a.command=='verify':return verify(folder,s)
    if a.command=='http':
        # Retain container/port/UID safety checks; HTTP readiness belongs in the
        # accumulating audit, otherwise its first failure would hide every case.
        import smoke
        try:
            verify(folder,s,readiness=False)
            compose(folder,'run','--rm','--no-deps','ops','preflight')
        except (RuntimeError, subprocess.CalledProcessError, OSError):
            # Fail closed without contacting HTTP; preserve the blocked inventory.
            return smoke.blocked_setup(folder/'ops')
        result=smoke.run(s,folder/'ops',loopback=True,key_file=folder/'secrets/ops/jwt-key')
        smoke.run(s,folder/'ops',loopback=True,key_file=folder/'secrets/ops/jwt-key',class_deletion=True)
        compose(folder,'run','--rm','--no-deps','ops','class-delete-audit')
        return result

def verify(folder,s,readiness=True):
    for service,uid in [('api','1654'),('db','999')]:
        cid=compose(folder,'ps','-q',service,capture=True)
        require(cid,'Missing '+service)
        obj=json.loads(output(['docker','inspect',cid]))[0]
        if readiness or service=='db':
            require(obj['State'].get('Health',{}).get('Status')=='healthy','Unhealthy '+service)
        else:
            require(obj['State'].get('Running') is True,'API container is not running')
        ports=obj['HostConfig'].get('PortBindings') or {}
        if service=='db':require(not ports,'MySQL unexpectedly published')
        else:require(ports=={'8080/tcp':[{'HostIp':'127.0.0.1','HostPort':str(s['port'])}]},'API bind is not loopback-only')
        status=compose(folder,'exec','-T',service,'cat','/proc/1/status',capture=True)
        require(re.search(r'^Uid:\s+'+uid+r'\s',status,re.M),'PID 1 is not the configured non-root user')
    if not readiness:
        print('Owned containers, API loopback-only, SQL unpublished, PID 1 non-root verified; HTTP readiness delegated to audit.')
        return
    connection=http.client.HTTPConnection('127.0.0.1',s['port'],timeout=6)
    try:
        connection.request('GET','/health/ready',headers={'Host':s['domain'],'X-Forwarded-Proto':'https'})
        require(connection.getresponse().status==200,'Host loopback readiness did not return HTTP 200')
    finally:connection.close()
    print('Owned containers healthy, host loopback readiness HTTP 200, API loopback-only, SQL unpublished, PID 1 non-root. Nginx/HTTPS still requires smoke.')

if __name__=='__main__':
    try:main()
    except (RuntimeError,subprocess.CalledProcessError,OSError) as error:
        print(str(error),file=sys.stderr);sys.exit(1)
