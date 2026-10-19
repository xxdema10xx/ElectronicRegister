"""Verify MT-3B TRX counters, preserved test identities and real bootstrap cases."""
from pathlib import Path
import xml.etree.ElementTree as ET
import json, hashlib, collections, re, csv

B=Path(__file__).resolve().parents[2]
E=B/'mt3b-evidence'
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
assert len(sets['baseline'])==387
assert len(sets['final-1'])==len(sets['final-2'])==426
assert sets['baseline']<=sets['final-1']==sets['final-2']
for run in ['final-1','final-2']:
    assert summary[run]['classCounts']['Mt3ProfileTests']==21
    assert summary[run]['classCounts']['Mt3aOwnershipTests']==39
    assert summary[run]['classCounts']['Mt2IdentityTests']==50
    assert summary[run]['classCounts']['Mt2MicrosoftIdentityTests']==11
    assert summary[run]['classCounts']['AuditBootstrapTests']==2
    for environment in ['Development','Production']:
        assert any('AuditBootstrapTests.RealProgram_StartsAuthenticatesDocumentsAndStops' in n and environment in n for n in sets[run])
summary['preservation']={'all387BaselineCasesInBothFinalRuns':True,'identicalFinalCaseSets':True,'newScopedMySqlCases':39,'totalFailed':0,'totalSkippedOrNotExecuted':0}
(E/'verification.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')


assert summary['final-1']['total']==426
assert summary['final-2']['total']==426
assert summary['final-1']['classCounts']['Mt3bSchoolTests']==39
print('Verified baseline 387, both final runs 426, all previous cases and both bootstraps preserved.')
