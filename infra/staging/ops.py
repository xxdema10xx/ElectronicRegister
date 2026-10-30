"""One-shot operator jobs inside the non-root tools container, never API startup."""
import contextlib
import datetime
try:
    import fcntl
except ImportError: # Pure rendering tests also run on Windows; operations never do.
    fcntl=None
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import uuid

ROOT=Path('/workspace')
STATE=Path('/state/ops')
PUBLIC=Path('/state/public')
MYSQL=json.loads((Path(__file__).with_name('mysql-image.json')).read_text())

def require(value,message):
    if not value:raise RuntimeError(message)

def sha(data):return hashlib.sha256(data).hexdigest().upper()

def saved(path,value):
    temporary=path.with_suffix(path.suffix+'.new')
    temporary.write_text(json.dumps(value,indent=2)+'\n')
    temporary.chmod(0o600)
    temporary.replace(path)

def process(args,input=None):
    p=subprocess.run([str(a) for a in args],input=input,text=True,capture_output=True)
    if p.returncode:
        with (STATE/'errors.log').open('a') as file:file.write(p.stderr+'\n')
        raise RuntimeError('Operator command failed; inspect private /state/ops/errors.log. No automatic DDL replay.')
    return p.stdout.strip()

def mysql(statement,review=False):
    cnf='/run/secrets/reviewer.cnf' if review else '/run/secrets/migrator.cnf'
    return process(['mysql','--defaults-extra-file='+cnf,'--batch','--raw','--skip-column-names','--default-character-set=utf8mb4'],statement)

def identity(config):
    require(config.get('format')==3 and config.get('mysql_version')==MYSQL['version'] and config.get('mysql_image')==MYSQL['image'], 'Wrong staging engine/state: use a fresh namespace')
    row=json.loads(mysql("SELECT JSON_OBJECT('version',VERSION(),'host',@@hostname,'port',@@port,'dir',@@datadir,'secure',@@global.require_secure_transport,'binlog',@@global.log_bin,'standard_fk',@@global.restrict_fk_on_non_standard_key,'openssl',@@global.version_ssl_library,'tls',@@global.tls_version,'id',(SELECT id FROM er_stage_control.identity))"))
    require(row==dict(version=MYSQL['version'],host='er-stage-db',port=3306,dir='/var/lib/mysql/',secure=1,binlog=0,standard_fk=1,
                      openssl=row.get('openssl'),tls='TLSv1.2',id=config['id']), 'Database identity/ownership/OpenSSL/secure transport mismatch')
    require(row['openssl'].startswith('OpenSSL '), 'OpenSSL runtime required')
    protocol=mysql("SHOW SESSION STATUS LIKE 'Ssl_version'").split('\t')[-1]
    cipher=mysql("SHOW SESSION STATUS LIKE 'Ssl_cipher'").split('\t')[-1]
    require(protocol=='TLSv1.2' and cipher, 'A successful TLS 1.2 session is required before DDL')
    saved(STATE/'database-identity.json',dict(row,protocol=protocol,cipher=cipher))

def checkout(config):
    require(process(['git','-C',ROOT,'rev-parse','HEAD'])==config['revision'],'Wrong published source revision')
    local=(ROOT/'migrations/release/manifest.json').read_bytes().replace(b'\r\n',b'\n')
    packaged=Path('/opt/release/migrations/release/manifest.json').read_bytes().replace(b'\r\n',b'\n')
    require(sha(local)==config['manifest'] and local==packaged,'Packaged SQL/source manifest mismatch')
    process(['node',ROOT/'tools/release-artifacts.cjs'])

class Journal:
    def __init__(self):
        self.path=STATE/'journal.json'
        self.records=json.loads(self.path.read_text()) if self.path.exists() else {}
    def apply(self,name,statement):
        fingerprint=sha(statement.encode())
        old=self.records.get(name)
        if old:
            require(old['hash']==fingerprint and old['state']=='done','Partial/drifted SQL step '+name+': manual diagnosis required, never replay')
            return
        self.records[name]=dict(state='started',hash=fingerprint,at=datetime.datetime.now(datetime.timezone.utc).isoformat())
        saved(self.path,self.records)
        mysql(statement)
        self.records[name]['state']='done'
        saved(self.path,self.records)

def literal(value):
    return 'CONVERT(0x'+str(value).encode().hex()+' USING utf8mb4)'

def provision(config):
    journal=Journal()
    if not journal.records:
        require(mysql("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='electronic_register_staging'")=='0','Nonempty application schema without this journal: refusing bootstrap')
    binding=json.loads((PUBLIC/'binding.json').read_text())
    bundle=STATE/'bundle'
    if not bundle.exists():process(['node',ROOT/'tools/prepare-release-sql.cjs',PUBLIC/'binding.json',bundle])
    require((bundle/'binding.json').exists(),'Incomplete derived bundle: inspect it without deleting/replaying automatically')
    user_sql=[]
    for user,file in [('er_api','api-password'),('er_review','review-password')]:
        password=Path('/run/secrets/'+file).read_text()
        require(len(password)==64 and all(c in '0123456789abcdef' for c in password),'Unexpected generated credential format')
        user_sql.append(f"CREATE USER '{user}'@'{binding['apiHost']}' IDENTIFIED WITH caching_sha2_password BY '{password}' REQUIRE SSL;")
    journal.apply('00-login-users','\n'.join(user_sql))
    derived=json.loads((bundle/'binding.json').read_text())
    for item in derived['records']:
        actual=(bundle/item['artifact']).read_text().replace('\r\n','\n').encode()
        require(sha(actual)==item['artifactHash'],'Derived SQL artifact changed')
    # Names are numeric-prefixed; 99 is required after 005 resets routine grants.
    for file in sorted(bundle.glob('*.sql')):journal.apply(file.name,file.read_text())
    preflight()

def preflight():
    result=process(['node',ROOT/'tools/release-preflight.cjs',PUBLIC/'preflight.json'])
    saved(STATE/'preflight-result.json',json.loads(result))


def compatibility():
    # Actual execution/metadata checks on the new staging DB, not a parser claim.
    preflight()
    base=json.loads((ROOT/'migrations/bootstrap/schema-contract.json').read_text())
    additive=json.loads((ROOT/'migrations/release/additive-contract.json').read_text())
    binding=json.loads((STATE/'bundle/binding.json').read_text())
    require(binding.get('mysqlSeries')=='8.4', 'Wrong SQL compatibility variant')
    manifest=json.loads((ROOT/'migrations/release/manifest.json').read_text())
    require([r['source'] for r in binding['records']]==manifest['requiredMigrations'],'Incomplete migration closure')
    journal=Journal().records
    steps=['00-login-users','00-principals.sql',*(r['artifact'] for r in binding['records']),'99-routine-grants.sql']
    require(all(journal.get(step,{}).get('state')=='done' for step in steps),'Incomplete staging migrations/provisioning')
    for record in binding['records']:
        statement=(STATE/'bundle'/record['artifact']).read_text()
        require(record['sourceHash']==manifest['files'][record['source']] and
                sha(statement.encode())==record['artifactHash']==journal[record['artifact']]['hash'],
                'Migration source/bundle/journal hash mismatch')
    routines=json.loads(mysql("SELECT JSON_ARRAYAGG(JSON_OBJECT('name',ROUTINE_NAME,'type',ROUTINE_TYPE,'definer',DEFINER,'security',SECURITY_TYPE,'access',SQL_DATA_ACCESS)) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA='electronic_register_staging'"))
    require(routines==[dict(name='er_schema_ready',type='PROCEDURE',definer=binding['definer'],security='DEFINER',access='READS SQL DATA')], 'Unexpected/missing procedure or stored function')
    counts=json.loads(mysql("SELECT JSON_OBJECT('tables',(SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='electronic_register_staging' AND TABLE_TYPE='BASE TABLE'),'triggers',(SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA='electronic_register_staging'))"))
    tables=len(set(base['tables'])|set(additive['tables']))
    triggers=len(set(base['triggers'])|{t['TRIGGER_NAME'] for t in additive['triggers']})
    require(counts==dict(tables=tables,triggers=triggers),'Unexpected/missing staging schema objects')
    accounts=json.loads(mysql("SELECT JSON_ARRAYAGG(JSON_OBJECT('user',User,'host',Host,'plugin',plugin,'ssl',ssl_type,'locked',account_locked)) FROM mysql.user WHERE User IN ('er_migrator','er_review','er_api','er_stage_definer')"))
    require(len(accounts)==4 and len({a['user'] for a in accounts})==4, 'Unexpected staging account topology')
    for account in accounts:
        require(account['plugin']=='caching_sha2_password', 'Legacy authentication plugin rejected')
        if account['user']=='er_stage_definer':
            require(account['host']=='%' and account['locked']=='Y', 'Definer must be locked')
        else:
            require(account['host']==binding['api'].split('@')[1] and account['ssl']=='ANY' and account['locked']=='N', 'Login TLS/host policy mismatch')
    saved(STATE/'compatibility-result.json',dict(version=MYSQL['version'],**counts,procedures=1,storedFunctions=0,
          definer=binding['definer'],migrations=[r['source'] for r in binding['records']],
          provisioningSteps=steps,accounts=accounts,preflightVerified=True,runtimeVerified=True))

def tls():
    preflight()
    password=Path('/run/secrets/api-password').read_text()
    config=dict(connectionString='Server=er-stage-db;Port=3306;Database=electronic_register_staging;User ID=er_api;Password='+password+';SslMode=VerifyFull;SslCa=/run/secrets/mysql-ca.pem;TlsVersion=TLS 1.2',wrongHost='er-stage-db-wrong',wrongCaFile='/run/secrets/wrong-ca.pem')
    saved(STATE/'transport-private.json',config)
    result=process(['dotnet','/opt/transport/ReleaseTransportProbe.dll',STATE/'transport-private.json'])
    saved(STATE/'transport-result.json',json.loads(result))

def insert(table,columns,values):
    return 'INSERT INTO `'+table+'`('+','.join('`'+c+'`' for c in columns)+') VALUES('+','.join(literal(v) for v in values)+');'

def seed_sql(seed,hashes):
    ids=seed['ids'];accounts=seed['accounts'];statements=['USE electronic_register_staging;','START TRANSACTION;']
    for value in [*ids.values(),*(a['id'] for a in accounts.values()),*(a['membership'] for a in accounts.values())]:uuid.UUID(value)
    for tenant in ['a','b']:
        owner=ids['tenant_'+tenant]
        statements.append(insert('institution',['id','name','code','status'],[owner,'Synthetic staging '+tenant,'STAGE_'+owner[:12],'active']))
        statements.append(insert('study_area',['id','institution_id','name','description'],[ids['area_'+tenant],owner,'Synthetic area','Staging only']))
        statements.append(insert('study_path',['id','institution_id','name'],[ids['path_'+tenant],owner,'Synthetic path']))
        statements.append(insert('biennium',['id','institution_id','start_year','end_year'],[ids['biennium_'+tenant],owner,'2100-09-01','2102-08-31']))
        statements.append(insert('biennium_study_area',['id','institution_id','biennium_id','study_area_id'],[ids['bsa_'+tenant],owner,ids['biennium_'+tenant],ids['area_'+tenant]]))
        statements.append(insert('biennium_study_path',['id','institution_id','biennium_study_area_id','study_path_id'],[ids['bsp_'+tenant],owner,ids['bsa_'+tenant],ids['path_'+tenant]]))
        statements.append(insert('class',['id','institution_id','biennium_study_path_id','name'],[ids['class_'+tenant],owner,ids['bsp_'+tenant],'Synthetic '+tenant]))
    owner=ids['tenant_a']
    statements.extend([
        insert('teacher',['id','institution_id','first_name','last_name'],[ids['teacher'],owner,'Synthetic','Teacher']),
        insert('student',['id','institution_id','class_id','first_name','last_name'],[ids['student'],owner,ids['class_a'],'Synthetic','Student']),
        insert('subject',['id','institution_id','name'],[ids['subject'],owner,'Staging subject']),
        insert('class_subject',['id','institution_id','class_id','subject_id','teacher_id'],[ids['assignment'],owner,ids['class_a'],ids['subject'],ids['teacher']])])
    for name,a in accounts.items():
        # user.role is historical required data only; authority is explicit below.
        statements.append(insert('user',['id','email','password_hash','role'],[a['id'],a['email'],hashes[name],'student']))
        statements.append(insert('institution_membership',['id','institution_id','user_id','status'],[a['membership'],ids['tenant_'+a['tenant']],a['id'],'active']))
        statements.append(insert('institution_membership_role',['membership_id','role'],[a['membership'],a['role']]))
        if name in ('teacher','student'):
            statements.append(insert('institution_'+name+'_profile',['institution_id','membership_id',name+'_id'],[owner,a['membership'],ids[name]]))
    statements.append('COMMIT;')
    return '\n'.join(statements)

def seed(config):
    preflight()
    data=json.loads((STATE/'seed.json').read_text())
    require(data['stage_id']==config['id'],'Wrong synthetic seed ownership')
    journal=Journal()
    if 'synthetic-seed' not in journal.records:
        require(mysql('SELECT (SELECT COUNT(*) FROM electronic_register_staging.user)+(SELECT COUNT(*) FROM electronic_register_staging.institution)')=='0','Seed only on this new empty staging schema')
    hash_file=STATE/'seed-hashes.json'
    if not hash_file.exists():saved(hash_file,json.loads(process(['dotnet','/opt/support/StagingSupport.dll','hashes',STATE/'seed.json'])))
    journal.apply('synthetic-seed',seed_sql(data,json.loads(hash_file.read_text())))
    saved(STATE/'seed-result.json',dict(stage=config['id'],syntheticAccounts=4,tenants=2))

def main():
    require(fcntl is not None,'Operator jobs require Linux file locks')
    require(len(sys.argv)==2 and sys.argv[1] in ('source','provision','compatibility','seed','preflight','tls','smoke'),'Explicit operator command required')
    STATE.mkdir(exist_ok=True,mode=0o700)
    with (STATE/'operation.lock').open('a') as lock:
        fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
        config=json.loads((PUBLIC/'staging.json').read_text())
        checkout(config)
        if sys.argv[1]=='source':
            saved(STATE/'source-result.json',dict(revision=config['revision'],manifest=config['manifest'],verified=True))
            print('Published source and packaged SQL closure verified; no database contacted.')
            return
        identity(config)
        if sys.argv[1]=='provision':provision(config)
        elif sys.argv[1]=='compatibility':compatibility()
        elif sys.argv[1]=='seed':seed(config)
        elif sys.argv[1]=='preflight':preflight()
        elif sys.argv[1]=='tls':tls()
        else:
            preflight()
            import smoke
            smoke.run(config,STATE)
        print(json.dumps({'command':sys.argv[1],'success':True,'stage':config['id']}))

if __name__=='__main__':
    try:main()
    except Exception as error:
        import traceback
        if STATE.exists():
            with (STATE/'errors.log').open('a') as file:file.write(traceback.format_exc()+'\n')
        # Full provider/DDL diagnostics stay in the protected state, not Compose stdout.
        print('Staging operation failed: '+type(error).__name__+'. Inspect private state; no replay/repair was attempted.',file=sys.stderr)
        sys.exit(1)
