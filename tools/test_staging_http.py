"""Offline audit-engine fault injection. No positive backend claim is made here."""
import base64
import copy
import hashlib
import hmac
import importlib.util
import io
import json
from pathlib import Path
import socket
import tempfile
import time
import unittest
from unittest import mock
import urllib.error
import uuid

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('staging_smoke',ROOT/'infra/staging/smoke.py')
smoke=importlib.util.module_from_spec(spec);spec.loader.exec_module(smoke)

class HttpAuditTests(unittest.TestCase):
    def test_separate_class_deletion_gate_keeps_original_plan_and_rejects_unprotected_delete(self):
        self.assertEqual(54,len(self.cases))
        cases=smoke.class_deletion_plan(self.seed,self.key)
        for case in cases:
            case['headers']={'X-Test-Case':case['name']}
        original=self.sender()
        statuses={'protected-class-delete':409,'foreign-class-delete':404,'deleted-class-missing':404}
        def send(method,path,body,token,headers):
            name=headers['X-Test-Case']
            if name in statuses:return statuses[name],{},b''
            return original(method,path,body,token,headers)
        result=smoke.report(smoke.execute(cases,send))
        self.assertEqual(dict(PASS=8,FAIL=0,BLOCKED=0),result['summary'])
        statuses['protected-class-delete']=204
        result=smoke.report(smoke.execute(cases,send))
        self.assertFalse(result['success'])
        self.assertEqual('FAIL',next(c for c in result['checks'] if c['name']=='protected-class-delete')['outcome'])

    def setUp(self):
        self.seed=smoke.synthetic_seed(); self.key='synthetic-signing-key'
        self.cases=smoke.plan(self.seed,self.key)
        self.sent=[]
        for case in self.cases:
            original=case['headers']
            case['headers']=lambda c,h=original,n=case['name']:{**(h(c) if callable(h) else h or {}),'X-Test-Case':n}

    def token(self,actor,selection=False):
        purpose='er-selection+jwt' if selection else 'er-institution+jwt'
        account=self.seed['accounts'][actor]
        body=dict(purpose=purpose,iss='er-staging',aud='er-staging-client:'+('institution-selection' if selection else 'institution-access'),sub=account['id'],exp=int(time.time())+600)
        if not selection: body['institution_id']=self.seed['ids']['tenant_'+account['tenant']]
        encode=lambda b:base64.urlsafe_b64encode(b).decode().rstrip('=')
        head=encode(json.dumps(dict(alg='HS256',typ=purpose)).encode()); payload=encode(json.dumps(body).encode())
        return head+'.'+payload+'.'+encode(hmac.new(self.key.encode(),(head+'.'+payload).encode(),hashlib.sha256).digest())

    def sender(self,failures=None):
        failures=failures or {}; by_name={c['name']:c for c in self.cases}; ids=self.seed['ids']
        resource=str(uuid.uuid4()); question=str(uuid.uuid4()); option=str(uuid.uuid4())
        def send(method,path,body,token,headers):
            name=headers['X-Test-Case']; self.sent.append((name,method,path,copy.deepcopy(body)))
            if name in failures:
                failure=failures[name]
                if isinstance(failure,Exception):raise failure
                return failure,{},b'password=secret not JSON'
            if name in ('class-update','class-stale-update'):self.assertEqual(headers['If-Match'],'"0"')
            case=by_name[name]; data=None; response_headers={}
            if name=='https-readiness':response_headers={'Strict-Transport-Security':'max-age=1','X-Content-Type-Options':'nosniff','X-Frame-Options':'DENY'}
            elif name=='class-read':response_headers={'ETag':'"0"'}
            elif name.startswith('login-'):data={'ticket':self.token(name[6:],True)}
            elif name.startswith('choices-'):
                a=self.seed['accounts'][name[8:]];data=[dict(institutionId=ids['tenant_'+a['tenant']],membershipId=a['membership'])]
            elif name.startswith('selection-') and name!='selection-cannot-access':data={'token':self.token(name[10:])}
            elif name.startswith('context-'):
                a=self.seed['accounts'][name[8:]];data=dict(institutionId=ids['tenant_'+a['tenant']],membershipId=a['membership'])
            elif name.startswith('calendar-configure-'):
                actor='admin_b' if name.endswith('admin_b') else 'admin_a'
                data={'revision':0,'eventId':body['eventId'],'resourceId':ids['tenant_'+self.seed['accounts'][actor]['tenant']]}
            elif name=='calendar-settings':data={'timeZoneId':'Europe/Rome','approvalLeadDays':7,'revision':0}
            elif name.startswith('classes-'):data={'items':[{'id':ids['class_a' if name.endswith('admin_a') else 'class_b']}]}
            elif name in ('class-write','teacher-create-exam'):data=resource
            elif name=='teacher-draft':data={'questions':[{'id':question,'options':[{'id':option,'isCorrect':True}]}]}
            elif name=='student-start':data={'submissionId':resource,'exam':{'questions':[{'options':[{'id':option}]}]}}
            elif name=='autosave':data={'accepted':True,'currentRevision':1}
            elif name in ('submit','submit-replay'):data={'passed':True,'grade':10}
            elif name=='official-grades':data=[{'studentId':ids['student'],'value':10,'subjectId':ids['subject'],'teacherId':ids['teacher']}]
            elif name=='foreign-grade-filter':data={'items':[]}
            elif name=='cors-allowed':response_headers={'Access-Control-Allow-Origin':'https://consoliprojects.dev'}
            return case['expected'],response_headers,b'' if data is None else json.dumps(data).encode()
        return send

    def test_inventory_is_complete_unique_and_dependencies_are_topological(self):
        expected={'https-readiness','liveness','anonymous-denied','selection-cannot-access','access-cannot-select','tampered-signature','foreign-membership-selection','foreign-class-hidden','calendar-settings','foreign-calendar-class-hidden','calendar-student-forbidden','legacy-exam-retired','legacy-grade-retired','class-write','class-read','class-update','class-stale-update','class-delete','student-write-forbidden','teacher-create-exam','foreign-exam-hidden','teacher-draft','publish','student-start','autosave','submit','submit-replay','foreign-submission-hidden','official-grades','foreign-grade-filter','cors-allowed','cors-denied'}
        expected.update(prefix+a for a in ('admin_a','admin_b','teacher','student') for prefix in ('login-','choices-','selection-','context-'))
        expected.update(prefix+a for a in ('admin_a','admin_b') for prefix in ('calendar-configure-','calendar-configure-replay-','classes-'))
        self.assertEqual({c['name'] for c in self.cases},expected)
        statuses={n:200 for n in expected}
        statuses.update({n:401 for n in ('anonymous-denied','selection-cannot-access','access-cannot-select','tampered-signature')})
        statuses.update({n:403 for n in ('foreign-membership-selection','student-write-forbidden','calendar-student-forbidden')})
        statuses.update({n:404 for n in ('foreign-class-hidden','foreign-calendar-class-hidden','legacy-exam-retired','legacy-grade-retired','foreign-exam-hidden','foreign-submission-hidden')})
        statuses['class-stale-update']=409
        statuses.update({n:201 for n in ('class-write','teacher-create-exam')})
        statuses.update({n:204 for n in ('class-update','class-delete','publish','cors-allowed','cors-denied')})
        self.assertEqual({c['name']:c['expected'] for c in self.cases},statuses)
        self.assertEqual(len(self.cases),len(expected)); seen=set()
        for case in self.cases:
            self.assertTrue(set(case['deps'])<=seen);seen.add(case['name'])
        self.assertEqual({c['scenario'] for c in self.cases},{'health','identity','authorization','calendar','classes','exams','submission','grades','cors'})

    def test_full_engine_plan_and_all_assertions_pass_with_contract_fixtures(self):
        result=smoke.report(smoke.execute(self.cases,self.sender()))
        self.assertTrue(result['success'],result)
        self.assertEqual(len(self.sent),len(self.cases))

    def test_independent_http_errors_accumulate_and_calendar_does_not_block_exams(self):
        checks=smoke.execute(self.cases,self.sender({'calendar-configure-admin_a':404,'class-read':500,'cors-denied':403}))
        results={c['name']:c for c in checks}
        self.assertEqual(results['calendar-configure-admin_a']['category'],'feature flag')
        self.assertEqual(results['calendar-configure-replay-admin_a']['outcome'],'BLOCKED')
        self.assertEqual(results['class-delete']['outcome'],'PASS')
        self.assertEqual(results['submit-replay']['outcome'],'PASS')
        self.assertEqual(smoke.report(checks)['summary'],{'PASS':len(checks)-7,'FAIL':3,'BLOCKED':4})
        self.assertFalse(smoke.report(checks)['success'])

    def test_readiness_failure_does_not_hide_other_independent_scenarios(self):
        checks=smoke.execute(self.cases,self.sender({'https-readiness':503}))
        results={c['name']:c for c in checks}
        self.assertEqual(results['https-readiness']['category'],'database')
        for name in ('liveness','calendar-configure-admin_a','class-delete','submit-replay','cors-denied'):
            self.assertEqual(results[name]['outcome'],'PASS')
        self.assertFalse(smoke.report(checks)['success'])

    def test_login_failure_only_blocks_that_actors_dependents_and_not_independent_calls(self):
        results={c['name']:c for c in smoke.execute(self.cases,self.sender({'login-teacher':401}))}
        self.assertEqual(results['teacher-create-exam']['outcome'],'BLOCKED')
        self.assertEqual(results['student-start']['outcome'],'BLOCKED')
        self.assertEqual(results['official-grades']['outcome'],'BLOCKED')
        for name in ('calendar-configure-admin_a','classes-admin_b','class-delete','foreign-grade-filter','cors-allowed'):
            self.assertEqual(results[name]['outcome'],'PASS')
        self.assertNotIn('teacher-create-exam',[s[0] for s in self.sent])

    def test_http_status_and_timeout_regressions_are_sanitized(self):
        for status,category in ((404,'feature flag'),(401,'autenticazione'),(403,'autorizzazione'),(400,'validazione'),(422,'validazione'),(500,'business logic'),(socket.timeout('password=secret Bearer jwt connection string'),'transport')):
            with self.subTest(status=type(status).__name__ if isinstance(status,Exception) else status):
                self.sent=[]
                result=smoke.report(smoke.execute(self.cases,self.sender({'calendar-configure-admin_a':status})))
                case=next(c for c in result['checks'] if c['name']=='calendar-configure-admin_a')
                self.assertEqual(case['outcome'],'FAIL'); self.assertEqual(case['category'],category)
                text=json.dumps(result)
                for secret in (self.key,'password=secret','Bearer','connection string',self.seed['accounts']['admin_a']['email'],self.seed['ids']['student']):self.assertNotIn(secret,text)
                self.assertFalse(result['success'])

    def test_every_assertion_failure_marks_its_case_failed_and_blocks_consumers(self):
        sender=self.sender()
        def broken(method,path,body,token,headers):
            status,h,data=sender(method,path,body,token,headers)
            if headers['X-Test-Case']=='autosave':data=b'{"accepted":false,"currentRevision":0}'
            return status,h,data
        results={c['name']:c for c in smoke.execute(self.cases,broken)}
        self.assertEqual(results['autosave']['outcome'],'FAIL')
        self.assertEqual(results['submit']['outcome'],'BLOCKED')
        self.assertEqual(results['submit-replay']['outcome'],'BLOCKED')
        self.assertEqual(results['foreign-submission-hidden']['outcome'],'PASS')

    def test_malformed_success_and_sensitive_body_never_leak_into_report(self):
        sender=self.sender()
        def broken(method,path,body,token,headers):
            status,h,data=sender(method,path,body,token,headers)
            if headers['X-Test-Case']=='login-admin_a':
                data=b'{"ticket":"JWT-secret", "email":"sensitive@example.invalid", "password":"password-secret"}'
                h={'Set-Cookie':'session=secret-cookie'}
            return status,h,data
        result=smoke.report(smoke.execute(self.cases,broken))
        self.assertFalse(result['success'])
        for value in ('JWT-secret','sensitive@example.invalid','password-secret','secret-cookie'):
            self.assertNotIn(value,json.dumps(result))
        self.assertEqual(next(c for c in result['checks'] if c['name']=='login-admin_a')['outcome'],'FAIL')

    def test_gate_rejects_blocked_only_empty_and_failed_results(self):
        self.assertFalse(smoke.report([])['success'])
        for status in ('FAIL','BLOCKED'):
            self.assertFalse(smoke.report([dict(scenario='health',outcome=status)])['success'])
        self.assertTrue(smoke.report([dict(scenario='health',outcome='PASS')])['success'])

    def test_mutations_are_unique_and_replays_reuse_exact_payload_without_retries(self):
        smoke.execute(self.cases,self.sender())
        calls={x[0]:x for x in self.sent}
        for actor in ('admin_a','admin_b'):
            self.assertEqual(calls['calendar-configure-'+actor][3],calls['calendar-configure-replay-'+actor][3])
        self.assertNotEqual(calls['calendar-configure-admin_a'][3]['eventId'],calls['calendar-configure-admin_b'][3]['eventId'])
        self.assertEqual(calls['submit'][3],calls['submit-replay'][3])
        self.assertEqual(calls['autosave'][3]['clientRevision'],1)
        self.assertEqual(calls['submit'][3]['clientRevision'],2)
        other=smoke.plan(self.seed,self.key)
        self.assertNotEqual(next(c for c in other if c['name']=='class-write')['body']({})['name'],calls['class-write'][3]['name'])
        self.sent=[]
        smoke.execute(self.cases,self.sender({'teacher-create-exam':TimeoutError('uncertain commit')}))
        self.assertEqual(sum(c[0]=='teacher-create-exam' for c in self.sent),1)
        self.assertNotIn('publish',[c[0] for c in self.sent])

    def test_real_entrypoint_saves_report_on_failure_and_refuses_reusing_namespace(self):
        with tempfile.TemporaryDirectory() as tmp:
            state=Path(tmp);(state/'seed.json').write_text(json.dumps(self.seed));(state/'seed-result.json').write_text('{}');(state/'key').write_text(self.key)
            config=dict(id='offline',domain='staging.example.invalid',port=18080)
            opener=mock.Mock();opener.open.side_effect=urllib.error.URLError('password=secret')
            with mock.patch.object(smoke.urllib.request,'build_opener',return_value=opener),mock.patch('sys.stdout',new_callable=io.StringIO):
                with self.assertRaises(smoke.AuditFailure):smoke.run(config,state,loopback=True,key_file=state/'key')
                self.assertTrue(list(state.glob('http-smoke-*.json')))
                count=opener.open.call_count
                with self.assertRaises(smoke.AuditFailure):smoke.run(config,state,loopback=True,key_file=state/'key')
                self.assertEqual(opener.open.call_count,count)
            reports=[json.loads(p.read_text()) for p in state.glob('http-smoke-*.json')]
            self.assertTrue(all(not r['success'] for r in reports))
            self.assertNotIn('secret',json.dumps(reports))
            self.assertTrue(any(r['summary']['BLOCKED']==len(self.cases) for r in reports))

    def test_pipeline_runs_real_audit_after_start_and_preserves_sanitized_failure_evidence(self):
        workflow=(ROOT/'.github/workflows/staging-package.yml').read_text()
        start=workflow.index('--state "$STAGE_STATE" start')
        audit=workflow.index('--state "$STAGE_STATE" http')
        self.assertLess(start,audit)
        self.assertNotIn('continue-on-error',workflow)
        self.assertIn('python3 tools/test_staging_http.py',workflow)
        self.assertIn('FullyQualifiedName~StagingCalendarFeatureTests',workflow)
        evidence=workflow.split('      - name: Collect sanitized HTTP evidence',1)[1].split('      - name: Sanitized Docker',1)[0]
        self.assertIn('if: always()',evidence)
        self.assertIn('actions/upload-artifact@',evidence)
        self.assertIn('http-smoke-*.json',evidence)
        self.assertNotIn('seed.json',evidence)
        self.assertNotIn('secrets/',evidence)

    def test_failed_host_safety_gate_saves_all_cases_blocked_and_never_sends_http(self):
        with tempfile.TemporaryDirectory() as tmp,mock.patch.object(smoke.urllib.request,'build_opener') as opener,mock.patch('sys.stdout',new_callable=io.StringIO):
            with self.assertRaises(smoke.AuditFailure):smoke.blocked_setup(Path(tmp))
            opener.assert_not_called()
            result=json.loads(next(Path(tmp).glob('http-smoke-*.json')).read_text())
            self.assertEqual(result['summary'],dict(PASS=0,FAIL=1,BLOCKED=len(self.cases)))
            self.assertEqual(result['checks'][0]['name'],'host-preconditions')

    def test_missing_setup_saves_failed_report_without_any_http_request(self):
        with tempfile.TemporaryDirectory() as tmp,mock.patch.object(smoke.urllib.request,'build_opener') as opener,mock.patch('sys.stdout',new_callable=io.StringIO):
            with self.assertRaises(smoke.AuditFailure):smoke.run(dict(id='offline',domain='staging.example.invalid',port=18080),Path(tmp),loopback=True)
            opener.return_value.open.assert_not_called()
            result=json.loads(next(Path(tmp).glob('http-smoke-*.json')).read_text())
            self.assertEqual(result['summary'],dict(PASS=0,FAIL=1,BLOCKED=len(self.cases)))

if __name__=='__main__':unittest.main()
