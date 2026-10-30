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

def run(config,state):
    require((state/'seed-result.json').exists(),'Run the explicit synthetic seed job first')
    seed=json.loads((state/'seed.json').read_text());ids=seed['ids'];key=Path('/run/secrets/jwt-key').read_text()
    require(seed['stage_id']==config['id'],'Seed belongs to another staging')
    base='https://'+config['domain']
    context=ssl.create_default_context() # Public Nginx certificate, CA/hostname validation enabled.
    opener=urllib.request.build_opener(NoRedirect(),urllib.request.HTTPSHandler(context=context))
    checks=[]
    def request(name,path,expected,method='GET',body=None,token=None,headers=None):
        hdr=dict(headers or {})
        if token:hdr['Authorization']='Bearer '+token
        if body is not None:hdr['Content-Type']='application/json'
        req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers=hdr,method=method)
        try:
            with opener.open(req,timeout=20) as response:status,response_headers,data=response.status,response.headers,response.read()
        except urllib.error.HTTPError as response:status,response_headers,data=response.code,response.headers,response.read()
        require(status==expected,f'{name}: expected {expected}, received {status}; response body intentionally omitted')
        checks.append(dict(case=name,status=status))
        return (json.loads(data) if data else None),response_headers
    _,headers=request('https-readiness','/health/ready',200,headers={'X-Forwarded-Proto':'http','X-Forwarded-For':'127.0.0.1'})
    require('max-age=' in headers.get('Strict-Transport-Security',''),'HTTPS proxy not recognized (HSTS missing)')
    request('liveness','/health/live',200)
    request('anonymous-denied','/api/institution/classes',401)
    tickets,tokens={},{}
    for name,account in seed['accounts'].items():
        login,_=request('login-'+name,'/api/Auth/identity/login',200,'POST',{'email':account['email'],'password':account['password']})
        ticket=login['ticket'];jwt(ticket,key,'er-selection+jwt',account['id']);tickets[name]=ticket
        choices,_=request('choices-'+name,'/api/Auth/institutions',200,token=ticket)
        tenant=ids['tenant_'+account['tenant']]
        require(any(c['institutionId']==tenant and c['membershipId']==account['membership'] for c in choices),'Expected explicit membership not offered')
        selected,_=request('selection-'+name,'/api/Auth/select-institution',200,'POST',{'institutionId':tenant,'membershipId':account['membership']},ticket)
        tokens[name]=selected['token'];jwt(tokens[name],key,'er-institution+jwt',account['id'],tenant)
        current,_=request('context-'+name,'/api/Auth/context',200,token=tokens[name])
        require(current['institutionId']==tenant and current['membershipId']==account['membership'],'HTTP tenant context mismatch')
    request('selection-cannot-access','/api/institution/classes',401,token=tickets['admin_a'])
    request('access-cannot-select','/api/Auth/institutions',401,token=tokens['admin_a'])
    parts=tokens['admin_a'].split('.');parts[2]=('A' if parts[2][0]!='A' else 'B')+parts[2][1:]
    request('tampered-signature','/api/institution/classes',401,token='.'.join(parts))
    request('foreign-membership-selection','/api/Auth/select-institution',403,'POST',{'institutionId':ids['tenant_b'],'membershipId':seed['accounts']['admin_b']['membership']},tickets['admin_a'])
    request('foreign-class-hidden','/api/institution/classes/'+ids['class_a'],404,token=tokens['admin_b'])
    for name in ('admin_a','admin_b'):
        page,_=request('classes-'+name,'/api/institution/classes',200,token=tokens[name])
        own=ids['class_'+seed['accounts'][name]['tenant']]
        foreign=ids['class_b' if name=='admin_a' else 'class_a']
        require(any(row['id']==own for row in page['items']) and all(row['id']!=foreign for row in page['items']),'Class list crossed tenant boundary')
    request('legacy-exam-retired','/api/Exam',404,token=tokens['admin_a'])
    request('legacy-grade-retired','/api/Grade',404,token=tokens['admin_a'])
    created,_=request('class-write','/api/institution/classes',201,'POST',{'name':'Smoke'+uuid.uuid4().hex[:8],'bienniumStudyPathId':ids['bsp_a']},tokens['admin_a'])
    request('class-read','/api/institution/classes/'+created,200,token=tokens['admin_a'])
    request('class-delete','/api/institution/classes/'+created,204,'DELETE',token=tokens['admin_a'])
    now=datetime.datetime.now(datetime.timezone.utc)
    date=lambda t:t.isoformat().replace('+00:00','Z')
    exam={'classSubjectId':ids['assignment'],'description':'Staging smoke '+uuid.uuid4().hex,'availableFrom':date(now-datetime.timedelta(minutes=5)),
          'availableUntil':date(now+datetime.timedelta(hours=1)),'durationMinutes':15,'passingPercent':60,
          'assignedStudents':[{'studentId':ids['student'],'extraTimeMinutes':0}],
          'questions':[{'position':1,'text':'2+2?','type':0,'points':1,'required':True,'options':[{'position':1,'text':'4','isCorrect':True},{'position':2,'text':'5','isCorrect':False}]}]}
    request('student-write-forbidden','/api/institution/exams',403,'POST',exam,tokens['student'])
    eid,_=request('teacher-create-exam','/api/institution/exams',201,'POST',exam,tokens['teacher'])
    request('foreign-exam-hidden','/api/institution/exams/'+eid,404,token=tokens['admin_b'])
    detail,_=request('teacher-draft','/api/institution/exams/'+eid,200,token=tokens['teacher'])
    question=detail['questions'][0];correct=next(o['id'] for o in question['options'] if o['isCorrect'])
    request('publish','/api/institution/exams/'+eid+'/publish',204,'POST',token=tokens['teacher'])
    started,_=request('student-start','/api/institution/exams/'+eid+'/start',200,'POST',token=tokens['student'])
    sid=started['submissionId']
    for q in started['exam']['questions']:
        require(all('isCorrect' not in option for option in q['options']),'Student option correctness leaked')
    answers=[{'questionId':question['id'],'selectedOptionIds':[correct]}]
    saved,_=request('autosave','/api/institution/submissions/'+sid+'/answers',200,'PUT',{'clientRevision':1,'answers':answers},tokens['student'])
    require(saved['accepted'] and saved['currentRevision']==1,'Autosave revision not committed')
    submission={'clientRevision':2,'answers':answers}
    first,_=request('submit','/api/institution/submissions/'+sid+'/submit',200,'POST',submission,tokens['student'])
    replay,_=request('submit-replay','/api/institution/submissions/'+sid+'/submit',200,'POST',submission,tokens['student'])
    require(first==replay and first['passed'] and first['grade']==10,'Submission/idempotency/grade mismatch')
    request('foreign-submission-hidden','/api/institution/submissions/'+sid+'/result',404,token=tokens['admin_b'])
    grades,_=request('official-grades','/api/institution/grades',200,token=tokens['admin_a'])
    require(sum(g['studentId']==ids['student'] for g in grades)>=1,'No official grade persisted')
    page,_=request('foreign-grade-filter','/api/institution/grades/paged?studentId='+ids['student'],200,token=tokens['admin_b'])
    require(not page['items'],'Foreign grade filter leaked data')
    _,allowed=request('cors-allowed','/api/institution/classes',204,'OPTIONS',headers={'Origin':'https://consoliprojects.dev','Access-Control-Request-Method':'GET'})
    require(allowed.get('Access-Control-Allow-Origin')=='https://consoliprojects.dev','Allowed CORS origin missing')
    _,denied=request('cors-denied','/api/institution/classes',204,'OPTIONS',headers={'Origin':'https://not-allowed.invalid','Access-Control-Request-Method':'GET'})
    require(denied.get('Access-Control-Allow-Origin') is None,'Unexpected CORS origin accepted')
    result={'stage':config['id'],'base':base,'httpsCertificateVerified':True,'jwtSignaturesVerified':True,'checks':checks}
    dest=state/('http-smoke-'+str(time.time_ns())+'.json');dest.write_text(json.dumps(result,indent=2)+'\n');dest.chmod(0o600)
