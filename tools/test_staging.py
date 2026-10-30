"""Local, non-Docker tests for staging guardrails. Real container/TLS tests are separate."""
import importlib.util
import io
import json
import re
import subprocess
from pathlib import Path
import tempfile
import unittest
import sys
from unittest import mock

ROOT=Path(__file__).resolve().parents[1]
def load(name):
    spec=importlib.util.spec_from_file_location('stage_'+name,ROOT/'infra/staging'/f'{name}.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
host=load('host');ops=load('ops');diagnostics=load('diagnostics')
sys.modules['diagnostics']=diagnostics

class StagingTests(unittest.TestCase):
    def test_http_audit_retains_container_safety_but_records_unhealthy_readiness_itself(self):
        state=dict(port=18080,project='synthetic',domain='staging.example.invalid')
        def compose(folder,*args,**kwargs):
            if args[0]=='ps':return args[-1]
            return 'Uid: 1654 1654 1654 1654' if args[2]=='api' else 'Uid: 999 999 999 999'
        def inspect(args):
            service=args[-1]
            return json.dumps([dict(State=dict(Running=True,Health=dict(Status='unhealthy' if service=='api' else 'healthy')),
                HostConfig=dict(PortBindings={'8080/tcp':[dict(HostIp='127.0.0.1',HostPort='18080')]} if service=='api' else {}))])
        with mock.patch.object(host,'compose',side_effect=compose),mock.patch.object(host,'output',side_effect=inspect),mock.patch.object(host.http.client,'HTTPConnection') as http,mock.patch('sys.stdout',new_callable=io.StringIO):
            host.verify(Path('synthetic'),state,readiness=False)
            http.assert_not_called()
            with self.assertRaisesRegex(RuntimeError,'Unhealthy api'):host.verify(Path('synthetic'),state)
            state['port']=18081
            with self.assertRaisesRegex(RuntimeError,'loopback-only'):host.verify(Path('synthetic'),state,readiness=False)

    def test_actual_preflight_failures_reach_github_projection(self):
        failures=json.loads(subprocess.check_output(['node',str(ROOT/'tools/test-mysql84.cjs'),'--diagnostics-json'],text=True))
        with tempfile.TemporaryDirectory() as tmp:
            folder=Path(tmp);(folder/'ops').mkdir()
            ops.phase('preflight')
            for check in failures:
                with mock.patch.object(ops,'STATE',folder/'ops'),mock.patch.object(ops.subprocess,'run') as run:
                    run.return_value=mock.Mock(returncode=1,stdout='',stderr='ER_PREFLIGHT_FAILURE '+json.dumps(dict(check,sql='secret-password')))
                    with self.assertRaises(RuntimeError):ops.preflight()
                safe=diagnostics.provisioning_evidence(folder)['failure']
                self.assertEqual(safe['check'],check)
                self.assertIsNone(safe['mysqlErrno'])
                self.assertNotIn('secret-password',json.dumps(safe))
            for check in ({'id':'unknown','category':'table','object':'secret-password'},
                          {'id':'base-table-ddl','category':'table','object':'secret-password'},
                          {'id':[],'category':'table','object':'audit_event'}):
                self.assertIsNone(diagnostics.preflight_check(check))

    def setUp(self):
        ops.phase('precheck')

    def test_sql_failure_metadata_never_contains_provider_text_or_secrets(self):
        with tempfile.TemporaryDirectory() as tmp,mock.patch.object(ops,'STATE',Path(tmp)),mock.patch.object(ops.subprocess,'run') as run:
            raw="CREATE USER secret_user IDENTIFIED BY 'fake-password';\nERROR 1064 (42000) at line 17: fake-password mysql://secret@host\n-----BEGIN PRIVATE KEY-----fake-key"
            run.return_value=mock.Mock(returncode=1,stderr=raw,stdout='fake-output-secret')
            ops.phase('migrations')
            with self.assertRaises(RuntimeError):ops.Journal().apply('01-001_schema.sql','secret SQL')
            evidence=json.loads((Path(tmp)/'operation-failure.json').read_text())
            self.assertEqual(evidence,dict(phase='migrations',step='01-001_schema.sql',exitCode=1,mysqlErrno=1064,sqlstate='42000',line=17))
            self.assertEqual(ops.Journal().records['01-001_schema.sql']['state'],'started')
            run.reset_mock()
            with self.assertRaises(RuntimeError):ops.Journal().apply('01-001_schema.sql','secret SQL')
            run.assert_not_called()
            # A child's SQL-looking text is not trusted as a mysql client error.
            self.assertIsNone(ops.failure_data(1,raw)['mysqlErrno'])
            for text in ('ERROR 1045 (28000): password=fake-secret','unknown fake-secret'):
                data=ops.failure_data(1,text,True)
                self.assertNotIn('fake-secret',json.dumps(data));self.assertIsNone(data['line'])

    def test_prejournal_failures_keep_their_phase(self):
        with tempfile.TemporaryDirectory() as tmp,mock.patch.object(ops,'STATE',Path(tmp)),mock.patch.object(ops,'PUBLIC',Path(tmp)),mock.patch.object(ops,'mysql',return_value='1'):
            with self.assertRaises(RuntimeError):ops.provision({})
            self.assertEqual(ops.failure_data()['phase'],'precheck')
            self.assertFalse((Path(tmp)/'journal.json').exists())
            (Path(tmp)/'binding.json').write_text('{}')
            with mock.patch.object(ops,'mysql',return_value='0'),mock.patch.object(ops,'process',side_effect=OSError('fake-secret')):
                with self.assertRaises(OSError):ops.provision({})
            self.assertEqual(ops.failure_data()['phase'],'bundle-generation')
            (Path(tmp)/'bundle').mkdir();(Path(tmp)/'bundle/binding.json').write_text('{}')
            with mock.patch.object(ops,'mysql',return_value='0'):
                with self.assertRaises(OSError):ops.provision({})
            self.assertEqual(ops.failure_data()['phase'],'account-creation')
            with mock.patch.object(ops,'process',side_effect=OSError('fake-secret')):
                with self.assertRaises(OSError):ops.preflight()
            self.assertEqual(ops.failure_data()['phase'],'preflight')

    def test_provisioning_collector_projects_only_safe_fields(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder=Path(tmp);(folder/'ops').mkdir()
            (folder/'ops/errors.log').write_text('fake-password raw SQL traceback')
            (folder/'ops/journal.json').write_text(json.dumps({'01-001_schema.sql':{'state':'started','sql':'fake-password'},'password=fake-secret':{'state':'started'}}))
            failure=dict(phase='migrations',step='01-001_schema.sql',exitCode=1,mysqlErrno=1064,sqlstate='42000',line=9,stderr='fake-password')
            (folder/'ops/operation-failure.json').write_text(json.dumps(failure))
            safe=diagnostics.provisioning_evidence(folder)
            self.assertEqual(safe['startedSteps'],['01-001_schema.sql'])
            self.assertEqual(safe['failure'],{k:v for k,v in failure.items() if k!='stderr'})
            for key in failure:failure[key]='fake-secret'
            (folder/'ops/operation-failure.json').write_text(json.dumps(failure))
            self.assertNotIn('fake-secret',json.dumps(diagnostics.provisioning_evidence(folder)))
            (folder/'ops/operation-failure.json').write_text('{invalid fake-secret')
            self.assertTrue(diagnostics.provisioning_evidence(folder)['operation-failure.json']['unavailable'])

    def test_preflight_child_metadata_is_bounded_and_sanitized(self):
        ops.phase('preflight')
        text='ER_SQL_FAILURE '+json.dumps(dict(step='preflight-query-1',exitCode=1,mysqlErrno=1142,sqlstate='42000',line=1,sql='fake-password'))
        self.assertEqual(ops.failure_data(1,text)['mysqlErrno'],1142)
        self.assertNotIn('fake-password',json.dumps(ops.failure_data(1,text)))

    def test_server_openssl_linkage_fails_closed_and_normalizes_aslr(self):
        for linkage in ('', 'libssl.so.3 => not found\nlibcrypto.so.3 => /lib/libcrypto.so.3', 'libcrypto.so.3 => /lib/libcrypto.so.3'):
            with self.assertRaises(RuntimeError):host.check_mysql_openssl(linkage)
        linkage='libssl.so.3 => /lib/libssl.so.3 (0x1234)\nlibcrypto.so.3 => /lib/libcrypto.so.3 (0xabcd)'
        self.assertEqual(host.check_mysql_openssl(linkage),host.check_mysql_openssl(linkage.replace('0x1234','0x5678')))

    def test_compiled_tls_capability_fails_closed(self):
        for help_text in ('tls-version TLSv1,TLSv1.1\n', '', 'tls-version TLSv1.20\n'):
            with self.assertRaises(RuntimeError):host.check_mysql_tls(help_text)
        host.check_mysql_tls('other option\ntls-version TLSv1,TLSv1.1,TLSv1.2\n')

    def test_staging_version_is_exact_and_historical_default_is_preserved(self):
        host.check_mysql_version('mysqld  Ver 8.4.13 for Linux on x86_64 (MySQL Community Server (GPL))')
        for version in ('5.7.24','8.4.130','8.4.13-custom','8.0.44'):
            with self.assertRaises(RuntimeError):host.check_mysql_version('mysqld  Ver '+version+' for Linux')
        self.assertIn("VERSION()='8.4.13'",(ROOT/'infra/staging/db-health.sh').read_text())
        self.assertEqual(host.MYSQL,ops.MYSQL)
        self.assertIn('policy.Local ? "5.7.24"', (ROOT/'Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Operations/OperationalPipeline.cs').read_text())
        self.assertEqual(json.loads((ROOT/'infra/release.runtime.example.json').read_text())['Readiness']['ExpectedVersion'],'5.7.24')

    def test_pinned_image_matches_registry_evidence_without_claiming_runtime(self):
        evidence=json.loads((ROOT/'docs/quality/staging-evidence/mysql-8.4-registry.json').read_text())
        self.assertEqual(host.MYSQL['image'],evidence['image'])
        self.assertEqual(evidence['image'],'mysql:8.4.13@'+evidence['amd64Digest'])
        self.assertEqual((evidence['os'],evidence['architecture']),('linux','amd64'))
        self.assertTrue(any('--uid 999 --gid 999' in h['created_by'] for h in evidence['history']))
        self.assertIn(':999:999:',evidence['staticFilesystem']['etc/passwd'])
        self.assertEqual(evidence['staticFilesystem']['etc/group'],'mysql:x:999:')
        self.assertFalse(evidence['containersExecuted'])
        self.assertFalse(evidence['runtimeTlsVerified'])

    def test_old_staging_states_are_never_upgraded(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder=Path(tmp)
            state=dict(format=3,mysql_version=host.MYSQL['version'],mysql_image=host.MYSQL['image'],id='a'*32,
                       project='er-stage-'+'a'*12,repository=str(host.ROOT))
            (folder/'state.json').write_text(json.dumps(state))
            self.assertEqual(host.load(folder)[1],state)
            for fields in ({'format':1},{'format':2},{'mysql_version':'5.7.24'},{'mysql_image':'mysql:5.7.24@sha256:'+'a'*64}):
                (folder/'state.json').write_text(json.dumps({**state,**fields}))
                with self.assertRaisesRegex(RuntimeError,'NEW isolated state'):host.load(folder)

    def test_prepare_binds_runtime_and_preflight_to_staging_only(self):
        import argparse
        with tempfile.TemporaryDirectory() as tmp, mock.patch.object(host.sys,'platform','linux'), mock.patch.object(host.os,'geteuid',return_value=0,create=True), mock.patch.object(host.os,'chown',create=True), mock.patch.object(host.shutil,'which',return_value='openssl'), mock.patch.object(host,'output',side_effect=['a'*40,'']), mock.patch.object(host,'run') as run, mock.patch('sys.stdout',new_callable=io.StringIO):
            folder=Path(tmp)/'new-state'
            def openssl(args,**kwargs):
                for option in ('-out','-keyout'):
                    if option in args:Path(args[args.index(option)+1]).write_text('synthetic-test-pem')
            run.side_effect=openssl
            args=argparse.Namespace(state=str(folder),domain='staging.example.invalid',subnet='172.30.252.0/24',port=18080,mysql_image=host.MYSQL['image'])
            host.prepare(args)
            runtime=json.loads((folder/'public/runtime.json').read_text())
            self.assertEqual(runtime['Readiness']['ExpectedVersion'],'8.4.13')
            self.assertEqual(runtime['Deployment'],dict(Mode='Staging',RolloutApproved=True))
            self.assertEqual({k for k,v in runtime['Multitenancy'].items() if v['Enabled']},
                             {'SchoolApi','CourseApi','ExamApi','MembershipAdminApi','MembershipWriteApi','InstitutionCalendar'})
            for feature in ('Onboarding','Governance','InstitutionWorker'):
                self.assertIs(runtime['Multitenancy'][feature]['Enabled'],False)
            for feature in ('FormativePlans','InstitutionImport'):
                self.assertFalse(runtime['Multitenancy'].get(feature,{}).get('Enabled',False))
            self.assertIs(runtime['Identity']['Legacy']['Enabled'],False)
            self.assertEqual(json.loads((folder/'public/preflight.json').read_text())['expectedVersion'],'8.4.13')
            self.assertEqual(json.loads((folder/'state.json').read_text())['format'],3)
            with self.assertRaises(RuntimeError):host.prepare(args)

    def test_diagnostics_redacts_generated_and_unexpected_credentials(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder=Path(tmp);(folder/'secrets/db').mkdir(parents=True)
            (folder/'ops').mkdir()
            (folder/'secrets/db/root.cnf').write_text('[client]\npassword=generated-root-password\n')
            (folder/'ops/seed.json').write_text(json.dumps({'accounts':{'a':{'password':'seed-aA!9'}}}))
            redact=diagnostics.sanitizer(folder)
            raw="generated-root-password seed-aA!9 password=unexpected Bearer abc.def.ghi IDENTIFIED BY 'sql-secret' ghp_unknownToken "
            raw+='-----BEGIN PRIVATE KEY-----\nprivate-body\n-----END PRIVATE KEY-----'
            safe=redact(raw)
            for secret in ('generated-root-password','seed-aA!9','unexpected','abc.def.ghi','sql-secret','ghp_unknownToken','private-body'):
                self.assertNotIn(secret,safe)

    def test_diagnostics_preserves_crash_health_evidence_without_env(self):
        with tempfile.TemporaryDirectory() as tmp, mock.patch.object(diagnostics.subprocess,'run') as run, mock.patch('sys.stdout',new_callable=io.StringIO) as stdout:
            obj={'Config':{'Labels':{'io.er.staging.id':'owned','com.docker.compose.service':'db'},'Env':['PASSWORD=must-not-escape']},
                 'State':{'Running':False,'ExitCode':1,'OOMKilled':True,'Health':{'Log':[{'ExitCode':1,'Output':'password=hidden\n::warning::untrusted'}]}},'RestartCount':3}
            def result(args,**kwargs):
                value='abc123def456\n' if args[1:3]==['ps','-aq'] else json.dumps([obj]) if args[1]=='inspect' else 'bounded log'
                return mock.Mock(returncode=0,stdout=value,stderr='')
            run.side_effect=result
            diagnostics.collect(Path(tmp),{'id':'owned','project':'er-stage-test'})
            lines=stdout.getvalue().splitlines()
            records=[json.loads(line) for line in lines]
            state=next(r['data'] for r in records if r['section']=='container-state')
            self.assertEqual(state['restartCount'],3);self.assertTrue(state['state']['OOMKilled'])
            self.assertNotIn('hidden',stdout.getvalue());self.assertNotIn('must-not-escape',stdout.getvalue())
            self.assertFalse(any(line.startswith('::') for line in lines))

    def test_diagnostics_refuses_foreign_containers_and_timeout_payloads(self):
        import subprocess
        with tempfile.TemporaryDirectory() as tmp, mock.patch.object(diagnostics.subprocess,'run') as run, mock.patch('sys.stdout',new_callable=io.StringIO) as stdout:
            foreign={'Config':{'Labels':{'io.er.staging.id':'foreign'}}}
            def result(args,**kwargs):
                if args[1]=='version':
                    raise subprocess.TimeoutExpired(args,15,output='password=timeout-secret')
                value='abc123def456' if args[1:3]==['ps','-aq'] else json.dumps([foreign]) if args[1]=='inspect' else ''
                return mock.Mock(returncode=0,stdout=value,stderr='')
            run.side_effect=result
            diagnostics.collect(Path(tmp),{'id':'owned','project':'er-stage-test'})
            self.assertNotIn('timeout-secret',stdout.getvalue())
            self.assertIn('"rejected": true',stdout.getvalue())
            self.assertFalse(any(call.args[0][1] in ('logs','exec') for call in run.call_args_list))

    def test_diagnostics_aborts_before_docker_if_credentials_cannot_be_read(self):
        with tempfile.TemporaryDirectory() as tmp, mock.patch.object(diagnostics.subprocess,'run') as run, mock.patch.object(diagnostics,'sanitizer',side_effect=PermissionError('unreadable credentials')):
            with self.assertRaises(PermissionError):
                diagnostics.collect(Path(tmp),{'id':'owned','project':'er-stage-test'})
            run.assert_not_called()

    def test_publication_and_no_remote_daemon(self):
        text=(ROOT/'infra/staging/compose.yaml').read_text()
        self.assertIn('127.0.0.1:${API_PORT:?required}:8080',text)
        db=text.split('  db:',1)[1].split('  api:',1)[0]
        self.assertNotIn('ports:',db)
        self.assertNotIn('docker.sock',text.replace('# Deliberately no ports, host networking or Docker socket.',''))
        self.assertIn("Only the local rootful Docker daemon",(ROOT/'infra/staging/host.py').read_text())

    def test_host_loopback_gate_requires_200_and_closes_connection(self):
        state={'port':18080,'domain':'staging.example.invalid'}
        def compose(folder,*args,**kwargs):
            if args[0]=='ps':return args[-1]
            return 'Uid:\t'+('1654' if args[2]=='api' else '999')+'\t999\t999\t999'
        def inspect(args):
            ports={'8080/tcp':[{'HostIp':'127.0.0.1','HostPort':'18080'}]} if args[-1]=='api' else {}
            return json.dumps([{'State':{'Health':{'Status':'healthy'}},'HostConfig':{'PortBindings':ports}}])
        with mock.patch.object(host,'compose',side_effect=compose),mock.patch.object(host,'output',side_effect=inspect),mock.patch.object(host.http.client,'HTTPConnection') as http,mock.patch('sys.stdout',new_callable=io.StringIO):
            http.return_value.getresponse.return_value.status=200
            host.verify(Path('unused'),state)
            http.return_value.request.assert_called_once_with('GET','/health/ready',headers={'Host':state['domain'],'X-Forwarded-Proto':'https'})
            http.return_value.close.assert_called_once()
            http.return_value.reset_mock()
            http.return_value.getresponse.return_value.status=302
            with self.assertRaisesRegex(RuntimeError,'HTTP 200'):host.verify(Path('unused'),state)
            http.return_value.close.assert_called_once()
    def test_inputs_are_restricted(self):
        self.assertEqual(str(host.validate('staging.consoliprojects.dev','172.30.252.0/24',18080)),'172.30.252.0/24')
        for domain,subnet,port in [('consoliprojects.dev','172.30.252.0/24',18080),('bad;nginx','172.30.252.0/24',18080),('a..dev','172.30.252.0/24',18080),('a.dev','0.0.0.0/0',18080),('a.dev','127.0.0.0/24',18080),('a.dev','172.30.252.0/24',3306)]:
            with self.assertRaises(RuntimeError):host.validate(domain,subnet,port)
    def test_nginx_is_additive_not_default_or_docker(self):
        state=dict(id='123456789abcdef0123456789abcdef0',domain='staging.consoliprojects.dev',project='er-stage-123456789abc',port=18080)
        http,tls=host.nginx(state,False),host.nginx(state,True)
        self.assertIn('listen 80;',http);self.assertNotIn('listen 80;',tls)
        self.assertIn('listen 443 ssl;',tls);self.assertIn('proxy_pass http://127.0.0.1:18080;',tls)
        self.assertNotIn('default_server',http+tls);self.assertNotIn('/etc/letsencrypt/',tls)
        self.assertIn('proxy_set_header X-Forwarded-For $remote_addr;',tls)
        self.assertNotIn('$request_uri',tls)
    def test_ddl_journal_is_idempotent_and_partial_failure_is_closed(self):
        with tempfile.TemporaryDirectory() as folder,mock.patch.object(ops,'STATE',Path(folder)),mock.patch.object(ops,'mysql') as sql:
            journal=ops.Journal();journal.apply('001','CREATE TABLE a(id int);');journal.apply('001','CREATE TABLE a(id int);')
            self.assertEqual(sql.call_count,1)
            with self.assertRaises(RuntimeError):journal.apply('001','DROP TABLE a;')
            sql.side_effect=RuntimeError('synthetic failure')
            with self.assertRaises(RuntimeError):journal.apply('002','CREATE TABLE b(id int);')
            sql.side_effect=None
            with self.assertRaises(RuntimeError):ops.Journal().apply('002','CREATE TABLE b(id int);')
            self.assertEqual(sql.call_count,2)
    def test_mysql_identity_is_exact_before_ddl(self):
        cfg={'id':'a'*32,'format':3,'mysql_version':ops.MYSQL['version'],'mysql_image':ops.MYSQL['image']}
        row=dict(version='8.4.13',host='er-stage-db',port=3306,dir='/var/lib/mysql/',secure=1,binlog=0,standard_fk=1,networking=0,
                 ca='/run/secrets/mysql-ca.pem',cert='/run/secrets/mysql-server.pem',key='/run/secrets/mysql-server-key.pem',tls='TLSv1.2',id=cfg['id'])
        with mock.patch.object(ops,'mysql',side_effect=[json.dumps(row),'Ssl_version\tTLSv1.2','Ssl_cipher\tECDHE-RSA-AES256-GCM-SHA384']),mock.patch.object(ops,'saved'):
            ops.identity(cfg)
        for field,value in [('id','foreign'),('secure',0),('binlog',1),('standard_fk',0),('version','5.7.24'),('version','8.0.0'),('dir','/real-data/'),('networking',1),('ca',''),('cert',''),('key',''),('tls','TLSv1.1')]:
            wrong={**row,field:value}
            with mock.patch.object(ops,'mysql',return_value=json.dumps(wrong)),self.assertRaises(RuntimeError):ops.identity(cfg)

    def test_identity_requires_negotiated_tls(self):
        cfg={'id':'a'*32,'format':3,'mysql_version':ops.MYSQL['version'],'mysql_image':ops.MYSQL['image']}
        row=dict(version='8.4.13',host='er-stage-db',port=3306,dir='/var/lib/mysql/',secure=1,binlog=0,standard_fk=1,networking=0,
                 ca='/run/secrets/mysql-ca.pem',cert='/run/secrets/mysql-server.pem',key='/run/secrets/mysql-server-key.pem',tls='TLSv1.2',id=cfg['id'])
        for protocol,cipher in [('Ssl_version\tTLSv1.1','Ssl_cipher\tcipher'),('Ssl_version\tTLSv1.2','Ssl_cipher\t'),('Ssl_version\tTLSv1.2','Ssl_cipher'),('Ssl_version\tTLSv1.2','')]:
            with mock.patch.object(ops,'mysql',side_effect=[json.dumps(row),protocol,cipher]),self.assertRaises(RuntimeError):
                ops.identity(cfg)

    def test_mysql_transport_is_explicit_tcp_with_certificate_verification(self):
        with mock.patch.object(ops,'process',return_value='1') as run:
            ops.mysql('SELECT 1')
        for flag in ('--protocol=TCP','--ssl-mode=VERIFY_IDENTITY','--tls-version=TLSv1.2'):
            self.assertIn(flag,run.call_args.args[0])

    def test_staging_sql_variables_are_mysql84_compatible(self):
        # New variables require review against the 8.4 manual and real sql-check gate.
        supported={'hostname','port','datadir','ssl_ca','ssl_cert','ssl_key','tls_version',
                   'require_secure_transport','skip_networking','log_bin','restrict_fk_on_non_standard_key'}
        removed={'version_ssl_library','have_ssl','have_openssl','default_authentication_plugin',
                 'query_cache_size','query_cache_type','expire_logs_days','tx_isolation','tx_read_only'}
        def validate(text):
            variables=set(re.findall(r'@@(?:(?:global|session)\.)?([a-z_0-9]+)',text,re.I))
            self.assertLessEqual(variables,supported)
            # Catch SHOW VARIABLES filters too: unknown names silently return no row.
            for variable in removed:
                self.assertIsNone(re.search(r'\b'+variable+r'\b',text,re.I))
        for file in (ROOT/'infra/staging').iterdir():
            if file.suffix in ('.py','.sh','.yaml'):validate(file.read_text())
        for variable in removed:
            with self.assertRaises(AssertionError):validate('SELECT @@global.'+variable)
            with self.assertRaises(AssertionError):validate("SHOW VARIABLES LIKE '"+variable+"'")
        workflow=(ROOT/'.github/workflows/staging-package.yml').read_text()
        self.assertIn('python3 tools/test_staging.py',workflow)
        self.assertIn('--state "$STAGE_STATE" sql-check',workflow)

    def test_compatibility_requires_complete_journal_and_exact_routines(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder=Path(tmp)
            binding=dict(mysqlSeries='8.4',artifactRoot=str(ROOT),database='electronic_register_staging',definerUser='er_stage_definer',definerHost='%',apiUser='er_api',apiHost='172.30.252.%',reviewUser='er_review',reviewHost='172.30.252.%')
            config=folder/'binding-input.json';config.write_text(json.dumps(binding))
            subprocess.run(['node',str(ROOT/'tools/prepare-release-sql.cjs'),str(config),str(folder/'bundle')],check=True,capture_output=True)
            journal={'00-login-users':dict(state='done',hash='synthetic-login-hash')}
            journal.update({p.name:dict(state='done',hash=ops.sha(p.read_text().encode())) for p in (folder/'bundle').glob('*.sql')})
            (folder/'journal.json').write_text(json.dumps(journal))
            routines=[dict(name='er_schema_ready',type='PROCEDURE',definer='er_stage_definer@%',security='DEFINER',access='READS SQL DATA')]
            with mock.patch.object(ops,'ROOT',ROOT), mock.patch.object(ops,'STATE',folder), mock.patch.object(ops,'preflight'), mock.patch.object(ops,'mysql') as sql, mock.patch.object(ops,'saved') as saved:
                sql.side_effect=[json.dumps(routines),json.dumps(dict(tables=71,triggers=180)),json.dumps([dict(user=u,host='%' if u=='er_stage_definer' else '172.30.252.%',plugin='caching_sha2_password',ssl='ANY',locked='Y' if u=='er_stage_definer' else 'N') for u in ('er_api','er_review','er_migrator','er_stage_definer')])]
                ops.compatibility()
                self.assertEqual(saved.call_args.args[1]['storedFunctions'],0)
                sql.side_effect=[json.dumps(routines+[dict(name='unexpected',type='FUNCTION')])]
                with self.assertRaisesRegex(RuntimeError,'stored function'):ops.compatibility()
                journal['99-routine-grants.sql']['state']='started'
                (folder/'journal.json').write_text(json.dumps(journal))
                sql.reset_mock()
                with self.assertRaisesRegex(RuntimeError,'Incomplete staging migrations'):ops.compatibility()
                sql.assert_not_called()
                journal['99-routine-grants.sql']['state']='done'
                (folder/'journal.json').write_text(json.dumps(journal))
                migration=next((folder/'bundle').glob('01-*.sql'))
                migration.write_text(migration.read_text()+'\n-- tampered\n')
                with self.assertRaisesRegex(RuntimeError,'hash mismatch'):ops.compatibility()
    def test_schema_audit_blocks_and_publishes_only_hash_records(self):
        record=dict(category='trigger',id='base-trigger-ddl',object='calendar_assignment_binding_bu',difference='ddl',expectedHash='A'*64,actualHash='B'*64)
        with mock.patch.object(ops.subprocess,'run',return_value=mock.Mock(returncode=1,stdout=json.dumps(record)+'\n',stderr='SQL secret')),mock.patch.object(ops,'saved') as saved,mock.patch('sys.stdout',new_callable=io.StringIO) as stdout:
            with self.assertRaisesRegex(RuntimeError,'staging blocked'):ops.audit()
            self.assertEqual(json.loads(stdout.getvalue()),record)
            self.assertEqual(saved.call_args.args[1],[record])
        with mock.patch.object(ops.subprocess,'run',return_value=mock.Mock(returncode=0,stdout='',stderr='')),mock.patch.object(ops,'saved'):
            ops.audit()

    def test_provision_audits_only_after_all_journal_steps(self):
        text=(ROOT/'infra/staging/ops.py').read_text()
        provision=text.split('def provision(config):',1)[1].split('def audit():',1)[0]
        self.assertLess(provision.index('journal.apply(file.name'),provision.index('    audit()'))
        self.assertLess(provision.index('    audit()'),provision.index('    preflight()'))
        # A started record is rejected by Journal before control can reach audit.
        with tempfile.TemporaryDirectory() as folder,mock.patch.object(ops,'STATE',Path(folder)),mock.patch.object(ops,'mysql') as sql:
            (Path(folder)/'journal.json').write_text(json.dumps({'migration':dict(state='started',hash=ops.sha(b'SQL'))}))
            with self.assertRaises(RuntimeError):ops.Journal().apply('migration','SQL')
            sql.assert_not_called()

    def test_seed_has_explicit_roles_owners_and_no_bypass(self):
        import uuid
        names=('tenant_a','tenant_b','biennium_a','biennium_b','area_a','area_b','path_a','path_b','bsa_a','bsa_b','bsp_a','bsp_b','class_a','class_b','teacher','student','subject','assignment')
        seed={'ids':{n:str(uuid.uuid4()) for n in names},'accounts':{}}
        for name,role,tenant in [('admin_a','InstitutionAdmin','a'),('admin_b','InstitutionAdmin','b'),('teacher','Teacher','a'),('student','Student','a')]:
            seed['accounts'][name]=dict(id=str(uuid.uuid4()),membership=str(uuid.uuid4()),role=role,tenant=tenant,email=name+'@example.invalid')
        sql=ops.seed_sql(seed,{n:'synthetic-hash' for n in seed['accounts']})
        self.assertIn('START TRANSACTION;',sql);self.assertTrue(sql.endswith('COMMIT;'))
        self.assertEqual(sql.count('INSERT INTO `institution_membership_role`'),4)
        self.assertIn('institution_teacher_profile',sql);self.assertIn('institution_student_profile',sql)
        self.assertNotIn('FOREIGN_KEY_CHECKS',sql);self.assertNotIn('fixture_owners',sql)
    def test_smoke_never_disables_https_and_covers_real_routes(self):
        text=(ROOT/'infra/staging/smoke.py').read_text()
        self.assertIn('ssl.create_default_context()',text)
        for name in ['tampered-signature','foreign-class-hidden','foreign-exam-hidden','foreign-submission-hidden','submit-replay','autosave','cors-denied']:
            self.assertIn(name,text)
        self.assertNotIn('_create_unverified_context',text)

if __name__=='__main__':unittest.main()
