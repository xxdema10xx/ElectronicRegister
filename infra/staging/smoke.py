"""Real HTTPS/API checks against this project's synthetic accounts. No JWT forging for positive cases."""
import base64
import datetime
import hashlib
import hmac
import json
from pathlib import Path
import ssl
import time
import urllib.error
import urllib.request
import uuid

def require(value,message):
    if not value:raise RuntimeError(message)

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):return None

def decode(value):return base64.urlsafe_b64decode(value+'='*(-len(value)%4))

def jwt(token,key,purpose,user,tenant=None):
    head,payload,signature=token.split('.')
    require(hmac.compare_digest(decode(signature),hmac.new(key.encode(),(head+'.'+payload).encode(),hashlib.sha256).digest()),'JWT signature invalid')
    header,body=json.loads(decode(head)),json.loads(decode(payload))
    require(header.get('alg')=='HS256' and header.get('typ')==purpose and body.get('purpose')==purpose,'JWT type/purpose mismatch')
    audience='er-staging-client:'+('institution-selection' if purpose=='er-selection+jwt' else 'institution-access')
    require(body.get('iss')=='er-staging' and body.get('aud')==audience and body.get('sub')==user and time.time()<body['exp']<=time.time()+1000,'JWT issuer/audience/subject/expiry mismatch')
    if tenant:require(body.get('institution_id')==tenant,'JWT tenant mismatch')
    return body

# A single declarative plan drives execution, offline contracts and documentation.
# Values, resolved URLs, headers, payloads and exception text never enter reports.
class AuditFailure(RuntimeError):
    pass


def feature_for(route):
    if '/calendar/' in route:
        return ['SchoolApi', 'InstitutionCalendar']
    if route.startswith('/api/institution/classes'):
        return ['SchoolApi']
    if route.startswith(('/api/institution/exams', '/api/institution/submissions', '/api/institution/grades')):
        return ['SchoolApi', 'ExamApi']
    return []


def plan(seed, key):
    ids = seed['ids']; accounts = seed['accounts']; cases = []
    def add(scenario, name, route, expected=200, method='GET', actor=None,
            deps=(), body=None, check=None, headers=None, path=None, token=None):
        auth = [] if actor is None else ['selection-'+actor]
        cases.append(dict(scenario=scenario, name=name, route=route, expected=expected,
                          method=method, actor=actor, deps=list(dict.fromkeys([*deps, *auth])),
                          body=body, check=check, headers=headers, path=path, token=token,
                          features=feature_for(route)))
    def access(c, actor): return c['selection-'+actor]['token']
    def ticket(c, actor): return c['login-'+actor]['ticket']
    def valid_guid(value):
        require(isinstance(value,str) and str(uuid.UUID(value))==value.lower(), 'Invalid resource identifier')
    def ready(data, headers, c):
        require('max-age=' in headers.get('Strict-Transport-Security',''), 'Missing HSTS')
        require(headers.get('X-Content-Type-Options')=='nosniff' and headers.get('X-Frame-Options')=='DENY', 'Missing security headers')
    add('health', 'https-readiness', '/health/ready', check=ready,
        headers={'X-Forwarded-Proto':'http','X-Forwarded-For':'127.0.0.1'})
    add('health', 'liveness', '/health/live')
    add('authorization', 'anonymous-denied', '/api/institution/classes', 401)
    for actor in ('admin_a','admin_b','teacher','student'):
        account=accounts[actor]; tenant=ids['tenant_'+account['tenant']]
        def login_check(data,h,c,a=account): jwt(data['ticket'],key,'er-selection+jwt',a['id'])
        add('identity','login-'+actor,'/api/Auth/identity/login',method='POST',
            body=lambda c,a=account:dict(email=a['email'],password=a['password']),check=login_check)
        def choices_check(data,h,c,a=account,t=tenant):
            require(any(x['institutionId']==t and x['membershipId']==a['membership'] for x in data),'Missing membership')
        add('identity','choices-'+actor,'/api/Auth/institutions',deps=['login-'+actor],
            token=lambda c,a=actor:ticket(c,a),check=choices_check)
        def selection_check(data,h,c,a=account,t=tenant): jwt(data['token'],key,'er-institution+jwt',a['id'],t)
        add('identity','selection-'+actor,'/api/Auth/select-institution',method='POST',deps=['choices-'+actor],
            token=lambda c,a=actor:ticket(c,a),body=lambda c,a=account,t=tenant:dict(institutionId=t,membershipId=a['membership']),check=selection_check)
        def context_check(data,h,c,a=account,t=tenant):
            require(data['institutionId']==t and data['membershipId']==a['membership'],'Context mismatch')
        add('identity','context-'+actor,'/api/Auth/context',actor=actor,check=context_check)
    add('authorization','selection-cannot-access','/api/institution/classes',401,deps=['login-admin_a'],token=lambda c:ticket(c,'admin_a'))
    add('authorization','access-cannot-select','/api/Auth/institutions',401,actor='admin_a')
    def tampered(c):
        parts=access(c,'admin_a').split('.'); parts[2]=('A' if parts[2][0]!='A' else 'B')+parts[2][1:]
        return '.'.join(parts)
    add('authorization','tampered-signature','/api/institution/classes',401,actor='admin_a',token=tampered)
    add('authorization','foreign-membership-selection','/api/Auth/select-institution',403,'POST',
        deps=['login-admin_a'],token=lambda c:ticket(c,'admin_a'),
        body=lambda c:dict(institutionId=ids['tenant_b'],membershipId=accounts['admin_b']['membership']))
    add('authorization','foreign-class-hidden','/api/institution/classes/{id:guid}',404,actor='admin_b',path=lambda c:'/api/institution/classes/'+ids['class_a'])
    for actor in ('admin_a','admin_b'):
        command=dict(eventId=str(uuid.uuid4()),expectedRevision=-1,timeZoneId='Europe/Rome',approvalLeadDays=7,reason='Synthetic staging probe')
        def receipt_check(d,h,c,a=actor,b=command):
            require(d['eventId']==b['eventId'] and d['resourceId']==ids['tenant_'+accounts[a]['tenant']] and d['revision']==0,'Calendar receipt mismatch')
        add('calendar','calendar-configure-'+actor,'/api/institution/calendar/settings',method='PUT',actor=actor,body=lambda c,b=command:b,check=receipt_check)
        def replay_check(data,h,c,a=actor): require(data==c['calendar-configure-'+a],'Calendar replay changed receipt')
        add('calendar','calendar-configure-replay-'+actor,'/api/institution/calendar/settings',method='PUT',actor=actor,
            deps=['calendar-configure-'+actor],body=lambda c,b=command:b,check=replay_check)
    add('calendar','calendar-settings','/api/institution/calendar/settings',actor='admin_a',deps=['calendar-configure-admin_a'],
        check=lambda d,h,c:require(d['timeZoneId']=='Europe/Rome' and d['approvalLeadDays']==7 and d['revision']==0,'Calendar settings mismatch'))
    add('calendar','foreign-calendar-class-hidden','/api/institution/calendar/months/{year:int}/{month:int}/classes/{classId:guid}',404,
        actor='admin_b',deps=['calendar-configure-admin_b'],path=lambda c:'/api/institution/calendar/months/2100/9/classes/'+ids['class_a'])
    # Also test the calendar role policy without performing another mutation.
    add('calendar','calendar-student-forbidden','/api/institution/calendar/settings',403,actor='student')
    for actor in ('admin_a','admin_b'):
        own=ids['class_'+accounts[actor]['tenant']]; foreign=ids['class_b' if actor=='admin_a' else 'class_a']
        def class_check(data,h,c,o=own,f=foreign):
            require(any(x['id']==o for x in data['items']) and all(x['id']!=f for x in data['items']),'Class list crossed tenant boundary')
        add('classes','classes-'+actor,'/api/institution/classes',actor=actor,check=class_check)
    add('authorization','legacy-exam-retired','/api/Exam',404,actor='admin_a')
    add('authorization','legacy-grade-retired','/api/Grade',404,actor='admin_a')
    class_body=dict(name='Smoke'+uuid.uuid4().hex[:8],bienniumStudyPathId=ids['bsp_a'])
    add('classes','class-write','/api/institution/classes',201,'POST',actor='admin_a',body=lambda c:class_body,check=lambda d,h,c:valid_guid(d))
    class_path=lambda c:'/api/institution/classes/'+c['class-write']
    add('classes','class-read','/api/institution/classes/{id:guid}',actor='admin_a',deps=['class-write'],path=class_path,
        check=lambda d,h,c:require(bool(h.get('ETag')),'Class ETag missing'))
    add('classes','class-update','/api/institution/classes/{id:guid}',204,'PUT',actor='admin_a',deps=['class-read'],path=class_path,
        headers=lambda c:{'If-Match':c['headers:class-read']['ETag']},body=lambda c:dict(name=class_body['name']+'U'))
    add('classes','class-stale-update','/api/institution/classes/{id:guid}',409,'PUT',actor='admin_a',deps=['class-update'],path=class_path,
        headers=lambda c:{'If-Match':c['headers:class-read']['ETag']},body=lambda c:dict(name=class_body['name']+'U'))
    # Delete remains safe if read/update fails: it targets only this run's created ID.
    add('classes','class-delete','/api/institution/classes/{id:guid}',204,'DELETE',actor='admin_a',deps=['class-write'],path=class_path)
    def exam_body(c):
        now=datetime.datetime.now(datetime.timezone.utc)
        date=lambda t:t.isoformat().replace('+00:00','Z')
        return dict(classSubjectId=ids['assignment'],description='Staging smoke '+str(uuid.uuid4()),
                    availableFrom=date(now-datetime.timedelta(minutes=5)),availableUntil=date(now+datetime.timedelta(hours=1)),
                    durationMinutes=15,passingPercent=60,assignedStudents=[dict(studentId=ids['student'],extraTimeMinutes=0)],
                    questions=[dict(position=1,text='2+2?',type=0,points=1,required=True,
                                    options=[dict(position=1,text='4',isCorrect=True),dict(position=2,text='5',isCorrect=False)])])
    add('exams','student-write-forbidden','/api/institution/exams',403,'POST',actor='student',body=exam_body)
    add('exams','teacher-create-exam','/api/institution/exams',201,'POST',actor='teacher',body=exam_body,check=lambda d,h,c:valid_guid(d))
    exam_path=lambda c:'/api/institution/exams/'+c['teacher-create-exam']
    add('exams','foreign-exam-hidden','/api/institution/exams/{id:guid}',404,actor='admin_b',deps=['teacher-create-exam'],path=exam_path)
    def draft_check(d,h,c):
        q=d['questions'][0]; valid_guid(q['id']); valid_guid(next(o['id'] for o in q['options'] if o['isCorrect']))
    add('exams','teacher-draft','/api/institution/exams/{id:guid}',actor='teacher',deps=['teacher-create-exam'],path=exam_path,check=draft_check)
    add('exams','publish','/api/institution/exams/{id:guid}/publish',204,'POST',actor='teacher',deps=['teacher-create-exam'],path=lambda c:exam_path(c)+'/publish')
    def start_check(d,h,c):
        valid_guid(d['submissionId'])
        require(all('isCorrect' not in o for q in d['exam']['questions'] for o in q['options']),'Student correctness leaked')
    add('submission','student-start','/api/institution/exams/{id:guid}/start',method='POST',actor='student',deps=['publish'],path=lambda c:exam_path(c)+'/start',check=start_check)
    sub_path=lambda c:'/api/institution/submissions/'+c['student-start']['submissionId']
    def answers(c,revision):
        q=c['teacher-draft']['questions'][0]
        return dict(clientRevision=revision,answers=[dict(questionId=q['id'],selectedOptionIds=[next(o['id'] for o in q['options'] if o['isCorrect'])])])
    def save_check(d,h,c): require(d['accepted'] and d['currentRevision']==1,'Autosave revision mismatch')
    add('submission','autosave','/api/institution/submissions/{id:guid}/answers',method='PUT',actor='student',deps=['student-start','teacher-draft'],
        path=lambda c:sub_path(c)+'/answers',body=lambda c:answers(c,1),check=save_check)
    def submit_check(d,h,c): require(d['passed'] and d['grade']==10,'Submission grade mismatch')
    add('submission','submit','/api/institution/submissions/{id:guid}/submit',method='POST',actor='student',deps=['autosave'],
        path=lambda c:sub_path(c)+'/submit',body=lambda c:answers(c,2),check=submit_check)
    add('submission','submit-replay','/api/institution/submissions/{id:guid}/submit',method='POST',actor='student',deps=['submit'],
        path=lambda c:sub_path(c)+'/submit',body=lambda c:answers(c,2),check=lambda d,h,c:require(d==c['submit'],'Submit replay changed result'))
    add('authorization','foreign-submission-hidden','/api/institution/submissions/{id:guid}/result',404,actor='admin_b',deps=['student-start'],path=lambda c:sub_path(c)+'/result')
    def grade_check(d,h,c):
        own=[g for g in d if g['studentId']==ids['student']]
        require(len(own)==1 and own[0]['value']==10 and own[0]['subjectId']==ids['subject'] and own[0]['teacherId']==ids['teacher'],'Official grade persistence mismatch')
    add('grades','official-grades','/api/institution/grades',actor='admin_a',deps=['submit'],check=grade_check)
    add('grades','foreign-grade-filter','/api/institution/grades/paged',actor='admin_b',path=lambda c:'/api/institution/grades/paged?studentId='+ids['student'],
        check=lambda d,h,c:require(not d['items'],'Foreign grade leaked'))
    add('cors','cors-allowed','/api/institution/classes',204,'OPTIONS',headers={'Origin':'https://consoliprojects.dev','Access-Control-Request-Method':'GET'},
        check=lambda d,h,c:require(h.get('Access-Control-Allow-Origin')=='https://consoliprojects.dev','Allowed CORS missing'))
    add('cors','cors-denied','/api/institution/classes',204,'OPTIONS',headers={'Origin':'https://not-allowed.invalid','Access-Control-Request-Method':'GET'},
        check=lambda d,h,c:require(h.get('Access-Control-Allow-Origin') is None,'CORS origin accepted'))
    return cases


def inventory(cases):
    return [{k:c[k] for k in ('scenario','name','route','method','expected','actor','deps','features')} for c in cases]


def category(case, status):
    # These are diagnostic hints, not claims inferred from sensitive response bodies.
    if status==404: return 'feature flag' if 'InstitutionCalendar' in case['features'] and case['expected']!=404 else 'routing'
    if status==401: return 'autenticazione'
    if status==403: return 'autorizzazione'
    if status in (400,422): return 'validazione'
    if status==409: return 'business logic'
    if status is not None and status>=500: return 'database' if case['route']=='/health/ready' else 'business logic'
    return 'assertion'


def execute(cases, send, prerequisite=True):
    results=[]; context={}; outcomes={}
    for case in cases:
        record={k:case[k] for k in ('scenario','name','method','route','expected')}
        record.update(received=None,outcome='BLOCKED',category='assertion',missing=[])
        missing=[d for d in case['deps'] if outcomes.get(d)!='PASS']
        if not prerequisite: missing=['setup']
        if missing:
            record['missing']=missing
        else:
            try:
                # Resolve only after dependencies pass. Never retry uncertain writes.
                path=case['path'](context) if case['path'] else case['route']
                body=case['body'](context) if case['body'] else None
                token=case['token'](context) if case['token'] else context['selection-'+case['actor']]['token'] if case['actor'] else None
                request_headers=case['headers'](context) if callable(case['headers']) else case['headers']
                status,headers,data=send(case['method'],path,body,token,request_headers)
                record['received']=status
                if status!=case['expected']:
                    record.update(outcome='FAIL',category=category(case,status))
                else:
                    # Error bodies are not needed for expected negative responses.
                    value=json.loads(data) if data and status<400 else None
                    if case['check']: case['check'](value,headers,context)
                    context[case['name']]=value
                    context['headers:'+case['name']]=headers
                    record['outcome']='PASS'
            except (TimeoutError,OSError,urllib.error.URLError):
                record.update(outcome='FAIL',category='transport')
            except Exception:
                # Fixed allowlisted metadata only: no exception text or traceback.
                record.update(outcome='FAIL',category='assertion')
        outcomes[case['name']]=record['outcome']; results.append(record)
    return results


def report(checks):
    scenarios={}
    for c in checks:
        scenarios.setdefault(c['scenario'],[]).append(c['outcome'])
    return dict(success=bool(checks) and all(c['outcome']=='PASS' for c in checks),
                summary={s:sum(c['outcome']==s for c in checks) for s in ('PASS','FAIL','BLOCKED')},
                scenarios={s:'FAIL' if 'FAIL' in v else 'BLOCKED' if 'BLOCKED' in v else 'PASS' for s,v in scenarios.items()},checks=checks)


def synthetic_seed():
    # Offline contract fixture only. Never used to send a positive runtime request.
    names=('tenant_a','tenant_b','class_a','class_b','bsp_a','assignment','student','subject','teacher')
    return dict(stage_id='offline',ids={n:str(uuid.uuid4()) for n in names},accounts={
        n:dict(id=str(uuid.uuid4()),membership=str(uuid.uuid4()),tenant='b' if n=='admin_b' else 'a',email=n+'@example.invalid',password='synthetic-only')
        for n in ('admin_a','admin_b','teacher','student')})


def run(config,state,loopback=False,key_file=None):
    state=Path(state); checks=[]; ready=False
    # Plan exists even if setup fails, so all dependent cases are reported BLOCKED.
    cases=plan(synthetic_seed(),'')
    try:
        require((state/'seed-result.json').exists(),'Seed missing')
        seed=json.loads((state/'seed.json').read_text())
        require(seed['stage_id']==config['id'],'Foreign seed')
        key=Path(key_file or '/run/secrets/jwt-key').read_text()
        cases=plan(seed,key)
        # One audit per fresh synthetic namespace. A timeout may have committed a
        # mutation; rerunning with a new event ID and revision -1 is unsafe.
        with (state/'http-audit-started').open('x') as marker: marker.write('started\n')
        ready=True
    except Exception:
        checks.append(dict(scenario='setup',name='setup',method=None,route=None,expected=None,received=None,outcome='FAIL',category='assertion',missing=[]))
    base=('http://127.0.0.1:'+str(config['port'])) if loopback else 'https://'+config['domain']
    try:
        context=ssl.create_default_context()
        opener=urllib.request.build_opener(NoRedirect(),urllib.request.HTTPSHandler(context=context))
        def send(method,path,body,token,headers):
            hdr=dict(headers or {})
            if loopback: hdr.update({'Host':config['domain'],'X-Forwarded-Proto':'https'})
            if token: hdr['Authorization']='Bearer '+token
            if body is not None: hdr['Content-Type']='application/json'
            req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers=hdr,method=method)
            try:
                with opener.open(req,timeout=20) as response: return response.status,response.headers,response.read()
            except urllib.error.HTTPError as response:
                return response.code,response.headers,response.read()
        checks.extend(execute(cases,send,prerequisite=ready))
    except Exception:
        checks.append(dict(scenario='setup',name='transport-setup',method=None,route=None,expected=None,received=None,outcome='FAIL',category='transport',missing=[]))
        checks.extend(execute(cases,None,prerequisite=False))
    return save_report(state,checks,loopback)


def blocked_setup(state,loopback=True):
    checks=[dict(scenario='setup',name='host-preconditions',method=None,route=None,expected=None,received=None,outcome='FAIL',category='assertion',missing=[])]
    checks.extend(execute(plan(synthetic_seed(),''),None,prerequisite=False))
    return save_report(Path(state),checks,loopback)


def save_report(state,checks,loopback):
    result=report(checks)
    result.update(httpsCertificateVerified=not loopback and result['success'],loopbackProxySimulation=loopback)
    dest=state/('http-smoke-'+str(time.time_ns())+'.json')
    with dest.open('x',encoding='utf-8') as file: json.dump(result,file,indent=2); file.write('\n')
    dest.chmod(0o600)
    print(json.dumps(dict(httpAudit=result['summary'],scenarios=result['scenarios'])))
    if not result['success']: raise AuditFailure('HTTP staging audit failed; see sanitized report')
    return result


if __name__=='__main__':
    import sys
    if sys.argv[1:]==['--contracts']:
        cases=plan(synthetic_seed(),'')
        output=inventory(cases)
        # Representative values exercise actual DTO deserialization offline.
        guid=str(uuid.uuid4())
        context={'teacher-draft':{'questions':[{'id':guid,'options':[{'id':guid,'isCorrect':True}]}]}}
        for item,case in zip(output,cases):
            if case['body']: item['payload']=case['body'](context)
        print(json.dumps(output))
    else:
        raise SystemExit('Use host.py for real HTTP; --contracts is offline only')
