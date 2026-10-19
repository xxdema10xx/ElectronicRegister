"""Verify TRX counters/test identities and backup integrity without exposing data."""
from pathlib import Path
import xml.etree.ElementTree as ET
import json, hashlib, collections, re, csv

B=Path(__file__).resolve().parents[2]
E=B/'mt3a-evidence'
NS={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
summary={}; sets={}
for run in ['baseline','final-1','final-2']:
    paths=sorted((E/run).glob('*.trx'))
    assert len(paths)==2,(run,len(paths))
    case_set=set(); class_counts=collections.Counter(); files=[]
    for path in paths:
        tree=ET.parse(path)
        counter={k:int(v) for k,v in tree.find('.//t:Counters',NS).attrib.items()}
        assert counter['total']==counter['executed']==counter['passed']
        assert counter.get('failed',0)==counter.get('notExecuted',0)==counter.get('error',0)==counter.get('timeout',0)==0,counter
        definitions={u.attrib['id']:u.find('t:TestMethod',NS).attrib['className'].split(',')[0] for u in tree.findall('.//t:TestDefinitions/t:UnitTest',NS)}
        results=tree.findall('.//t:Results/t:UnitTestResult',NS)
        assert len(results)==counter['total']
        for r in results:
            assert r.attrib['outcome']=='Passed'
            name=definitions[r.attrib['testId']]+'.'+r.attrib['testName']
            assert name not in case_set,name
            case_set.add(name);class_counts[definitions[r.attrib['testId']].rsplit('.',1)[-1]]+=1
        files.append({'file':run+'/'+path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'counters':counter})
    sets[run]=case_set
    summary[run]={'total':len(case_set),'files':files,'classCounts':dict(sorted(class_counts.items()))}
assert len(sets['baseline'])==348
assert len(sets['final-1'])==len(sets['final-2'])==387
assert sets['baseline']<=sets['final-1']==sets['final-2']
for run in ['final-1','final-2']:
    assert summary[run]['classCounts']['Mt3ProfileTests']==21
    assert summary[run]['classCounts']['Mt3aOwnershipTests']==39
    assert summary[run]['classCounts']['Mt2IdentityTests']==50
    assert summary[run]['classCounts']['Mt2MicrosoftIdentityTests']==11
    assert summary[run]['classCounts']['AuditBootstrapTests']==2
    for environment in ['Development','Production']:
        assert any('AuditBootstrapTests.RealProgram_StartsAuthenticatesDocumentsAndStops' in n and environment in n for n in sets[run])
summary['preservation']={'all348BaselineCasesInBothFinalRuns':True,'identicalFinalCaseSets':True,'newMySqlIntegrityCases':39,'totalFailed':0,'totalSkippedOrNotExecuted':0}
(E/'verification.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')

metadata=[]
private=E/'private'
for line in (private/'ddl-backups.jsonl').read_text(encoding='utf-8').splitlines():
    entry=json.loads(line);path=private/entry['backup']
    raw=path.read_bytes()
    assert hashlib.sha256(raw).hexdigest().upper()==entry['sha256']
    content=raw.decode('utf-8-sig')
    assert 'Dump completed' in content
    tables=set(re.findall(r'CREATE TABLE `(\w+)`',content))
    assert len(tables) in [27,28],len(tables)
    assert {'user','audit_event','submission_answer_option','institution_student_profile','institution_teacher_profile'}<=tables
    metadata.append({'file':entry['backup'],'sha256':entry['sha256'],'database':entry['database'],'version':entry['version'],'strictMode':'STRICT_' in entry['mode'],'tables':len(tables),'readableAndComplete':True})
assert all(e['database']=='electronic_register_test' and e['version']=='5.7.24' and e['strictMode'] for e in metadata)
(E/'backup-verification.json').write_text(json.dumps({'preDdlBackups':len(metadata),'allDigestsVerified':True,'allReadableCompleteStrictTestDatabase':True,'restoreTested':False,'backups':metadata},indent=2)+'\n',encoding='utf-8')
sql_dir=B.parents[1]/'migrations'/'mt3'
expected={}
for script in ['007_ownership_triggers.sql','009_explicit_platform_audit.sql']:
    for batch in (sql_dir/script).read_text(encoding='utf-8-sig').split('-- MT3A-BATCH'):
        match=re.search(r'CREATE TRIGGER (\w+) BEFORE (?:INSERT|UPDATE) ON `?\w+`? FOR EACH ROW (BEGIN.*END);',batch,re.S)
        if match: expected[match[1]]=re.sub(r'\s+',' ',match[2]).strip()
with (E/'schema-triggers.tsv').open(encoding='utf-8-sig',newline='') as f:
    actual={r['TRIGGER_NAME']:re.sub(r'\s+',' ',r['ACTION_STATEMENT'].replace('\\n','\n').replace('\\r','\r').replace('\\t','\t')).strip().rstrip(';') for r in csv.DictReader(f,delimiter='\t')}
assert len(expected)==len(actual)==40
assert expected==actual,'Versioned trigger bodies differ from actual MySQL definitions'
sql_preservation={'all40ActualTriggerBodiesMatchVersionedSqlIgnoringFormatting':True,'partialMt3SqlUnchanged':{}}
for script in ['001_profile_expand.sql','002_profile_backfill.sql']:
    original=B/'mt3-staging'/'sql'/script
    assert original.read_bytes()==(sql_dir/script).read_bytes(),script
    sql_preservation['partialMt3SqlUnchanged'][script]=hashlib.sha256(original.read_bytes()).hexdigest()
(E/'sql-preservation.json').write_text(json.dumps(sql_preservation,indent=2)+'\n',encoding='utf-8')
print('Verified: baseline 348; both final runs 387/387; all prior cases retained; Dev/Production bootstrap; '+str(len(metadata))+' private pre-DDL backups intact.')
