"""Local, non-Docker tests for staging guardrails. Real container/TLS tests are separate."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

ROOT=Path(__file__).resolve().parents[1]
def load(name):
    spec=importlib.util.spec_from_file_location('stage_'+name,ROOT/'infra/staging'/f'{name}.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
host=load('host');ops=load('ops')

class StagingTests(unittest.TestCase):
    def test_publication_and_no_remote_daemon(self):
        text=(ROOT/'infra/staging/compose.yaml').read_text()
        self.assertIn('127.0.0.1:${API_PORT:?required}:8080',text)
        db=text.split('  db:',1)[1].split('  api:',1)[0]
        self.assertNotIn('ports:',db)
        self.assertNotIn('docker.sock',text.replace('# Deliberately no ports, host networking or Docker socket.',''))
        self.assertIn("Only the local rootful Docker daemon",(ROOT/'infra/staging/host.py').read_text())
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
        cfg={'id':'a'*32}
        row=dict(version='5.7.24',host='er-stage-db',port=3306,dir='/var/lib/mysql/',secure=1,id=cfg['id'])
        with mock.patch.object(ops,'mysql',side_effect=[json.dumps(row),'Ssl_version\tTLSv1.2','Ssl_cipher\tECDHE-RSA-AES256-GCM-SHA384']),mock.patch.object(ops,'saved'):
            ops.identity(cfg)
        for field,value in [('id','foreign'),('secure',0),('version','8.0.0'),('dir','/real-data/')]:
            wrong={**row,field:value}
            with mock.patch.object(ops,'mysql',return_value=json.dumps(wrong)),self.assertRaises(RuntimeError):ops.identity(cfg)
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
